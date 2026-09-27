using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Плаваюча кнопка «Зібрати все · N» над нижнім меню. З'являється, коли
    /// дозріло 2+ рослини (одну швидше зібрати тапом по грядці).
    /// </summary>
    public partial class EconomyOverlayView
    {
        [Header("Сад")]
        public GardenManager garden;

        private const int HarvestAllMinReady = 2;
        private ClaimButton _harvestAll;

        private void BuildHarvestAllButton()
        {
            _harvestAll = MakeClaimButton(_overlayRoot, "", () =>
            {
                if (garden != null) garden.HarvestAll();
            });
            Place((RectTransform)_harvestAll.button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 108), new Vector2(190, 42));
            _harvestAll.button.transform.SetAsFirstSibling(); // під модалками й тостами
            GameEvents.OnPlotStateChanged += HandlePlotStateChanged;
        }

        private void OnDestroy()
        {
            GameEvents.OnPlotStateChanged -= HandlePlotStateChanged;
        }

        private void HandlePlotStateChanged(int slotIndex) => RefreshHarvestAll();

        private void RefreshHarvestAll()
        {
            if (_harvestAll == null) return;
            int ready = garden != null ? garden.ReadyCount : 0;
            bool show = ready >= HarvestAllMinReady;
            _harvestAll.button.gameObject.SetActive(show);
            if (show) SetClaim(_harvestAll, true, $"Зібрати все · {ready}");
        }
    }
}
