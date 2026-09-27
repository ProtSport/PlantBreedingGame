using TMPro;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Вікно лікування — тап на хвору рослину (GardenManager.SickPlotTapped).
    /// Шкідник: велика кнопка, яку треба тапнути N разів (безкоштовно).
    /// Хвороба: купити засіб за монети (PlantAilments.RemedyCost).
    /// </summary>
    public partial class EconomyOverlayView
    {
        private GameObject _treatModal;
        private TMP_Text _treatTitle, _treatDesc, _treatNote;
        private ClaimButton _treatAction;
        private PlotSlot _treatSlot;

        private void BuildTreatmentModal()
        {
            _treatModal = MakeModal("TreatmentModal", CloseTreatment, out var panel);

            _treatTitle = MakeLabel(panel, "Title", "", fontHead, 26, UIColors.Text, FontStyles.Normal);
            SetHeight(_treatTitle.gameObject, 32);
            _treatTitle.margin = new Vector4(0, 0, 40, 0); // місце під хрестик
            _treatTitle.enableAutoSizing = true; _treatTitle.fontSizeMin = 16; _treatTitle.fontSizeMax = 26;

            _treatDesc = MakeLabel(panel, "Desc", "", fontUi, 12, UIColors.Soft, FontStyles.Normal);
            _treatDesc.textWrappingMode = TextWrappingModes.Normal;
            SetHeight(_treatDesc.gameObject, 34);

            _treatNote = MakeLabel(panel, "Note", "Поки рослина хворіє, вона не росте.", fontUi, 11, UIColors.GoldLt, FontStyles.Bold);
            SetHeight(_treatNote.gameObject, 16);

            _treatAction = MakeClaimButton(panel, "", OnTreatAction);
            SetHeight(_treatAction.button.gameObject, 52);

            var close = MakeClaimButton(panel, "Закрити", CloseTreatment);
            SetHeight(close.button.gameObject, 40);
            close.bg.color = UIColors.Rgba(Color.white, 0.08f);
        }

        private void OpenTreatment(PlotSlot slot)
        {
            if (_treatModal == null || slot == null || slot.state != PlotState.Sick) return;
            _treatSlot = slot;
            _treatModal.SetActive(true);
            RefreshTreatment();
        }

        private void CloseTreatment()
        {
            _treatSlot = null;
            if (_treatModal != null) _treatModal.SetActive(false);
        }

        private void OnTreatAction()
        {
            if (_treatSlot == null) return;
            bool cured = _treatSlot.CurrentAilment.isPest
                ? EconomyService.TapPest(_treatSlot)
                : EconomyService.TryCureWithRemedy(_treatSlot);
            if (cured) CloseTreatment();
            else RefreshTreatment();
        }

        private void RefreshTreatment()
        {
            if (_treatSlot == null || _treatModal == null || !_treatModal.activeSelf) return;
            if (_treatSlot.state != PlotState.Sick || _treatSlot.plant == null)
            {
                CloseTreatment();
                return;
            }

            var ailment = _treatSlot.CurrentAilment;
            _treatTitle.text = $"{ailment.name} · {_treatSlot.plant.displayName}";
            _treatDesc.text = ailment.description;

            if (ailment.isPest)
            {
                SetClaim(_treatAction, true, $"Прибрати · ще {_treatSlot.pestTapsLeft} {TapsWord(_treatSlot.pestTapsLeft)}");
                _treatAction.bg.color = UIColors.Green;
                return;
            }

            int cost = EconomyService.RemedyCost(_treatSlot);
            var gm = GameManager.Instance;
            bool canPay = gm != null && gm.playerData.coins >= cost;
            SetClaim(_treatAction, canPay, canPay
                ? $"{ailment.remedyName} · {cost} монет"
                : $"{ailment.remedyName}: потрібно {cost} монет");
            if (canPay) _treatAction.bg.color = UIColors.GoldLt;
        }

        private static string TapsWord(int n)
        {
            int mod100 = n % 100, mod10 = n % 10;
            if (mod100 >= 11 && mod100 <= 14) return "тапів";
            if (mod10 == 1) return "тап";
            if (mod10 >= 2 && mod10 <= 4) return "тапи";
            return "тапів";
        }
    }
}
