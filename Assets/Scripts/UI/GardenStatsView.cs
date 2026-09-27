using TMPro;
using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>Лічильники "N ростуть / N готові" у шапці екрану саду.</summary>
    public class GardenStatsView : MonoBehaviour
    {
        public GardenManager manager;
        public TMP_Text growingCountLabel;
        public TMP_Text readyCountLabel;

        private void OnEnable()
        {
            GameEvents.OnPlotStateChanged += HandlePlotStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnPlotStateChanged -= HandlePlotStateChanged;
        }

        // Start (не OnEnable) — гарантовано після GardenManager.Start(), яка будує
        // сітку слотів (Plots); інакше перший Redraw() застає порожній список.
        private void Start()
        {
            Redraw();
        }

        private void HandlePlotStateChanged(int slotIndex) => Redraw();

        private void Redraw()
        {
            if (manager == null) return;
            int growing = 0, ready = 0;
            foreach (var plot in manager.Plots)
            {
                if (plot.state == PlotState.Growing || plot.state == PlotState.NeedsWater || plot.state == PlotState.Sick) growing++;
                else if (plot.state == PlotState.Ready) ready++;
            }
            if (growingCountLabel != null) growingCountLabel.text = growing.ToString();
            if (readyCountLabel != null) readyCountLabel.text = ready.ToString();
        }
    }
}
