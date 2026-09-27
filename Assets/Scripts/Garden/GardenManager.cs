using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Save;
using PlantBreeding.UI;

namespace PlantBreeding.Garden
{
    /// <summary>
    /// Головний контролер екрану "Мій сад" (розділ 5.1 GDD).
    /// MVP: одна зона, 6 слотів (2×3). Які з них відкриті — EconomyService
    /// (рівень гравця або покупка за монети). Стан грядок відновлюється зі
    /// збереження і записується після кожної зміни стану.
    /// Прив'язується до Canvas сцени MainGarden.
    /// </summary>
    public class GardenManager : MonoBehaviour
    {
        private const string PlantingSceneName = "Planting";

        [Header("Налаштування сітки")]
        public int gridSize = 6;
        [Tooltip("Застаріле: відкритість грядок тепер визначає EconomyConfig.PlotRules")]
        public int initiallyUnlocked = 4;

        [Header("UI")]
        public PlotSlotView plotViewPrefab;
        public Transform gridParent;

        private readonly List<PlotSlot> _plots = new List<PlotSlot>();
        private readonly List<PlotSlotView> _views = new List<PlotSlotView>();

        public IReadOnlyList<PlotSlot> Plots => _plots;

        /// <summary>Тап на рослину, що росте → EconomyOverlayView показує вікно прискорення.</summary>
        public static event System.Action<PlotSlot> GrowingPlotTapped;

        /// <summary>Тап на хвору рослину → EconomyOverlayView показує вікно лікування.</summary>
        public static event System.Action<PlotSlot> SickPlotTapped;

        /// <summary>Поточний сад (для push-сповіщень при згортанні гри).</summary>
        public static GardenManager Current { get; private set; }

        private float _tickAccumulator;
        private const float TickIntervalSeconds = 1f;

        private void OnEnable()
        {
            Current = this;
            GameEvents.OnLevelUp += HandleLevelUp;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            GameEvents.OnLevelUp -= HandleLevelUp;
        }

        private void Start()
        {
            BuildGrid();
            // Відновлені зі сейва стани не шлють подій — повідомити лічильники/кнопки один раз.
            GameEvents.RaisePlotStateChanged(-1);
        }

        private void HandleLevelUp(int newLevel) => RefreshUnlocks();

        private void RefreshUnlocks()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            foreach (var plot in _plots)
            {
                if (plot.state == PlotState.Locked && EconomyService.IsPlotUnlocked(gm.playerData, plot.slotIndex))
                    plot.Unlock();
            }
        }

        private void Update()
        {
            _tickAccumulator += Time.deltaTime;
            if (_tickAccumulator < TickIntervalSeconds) return;
            _tickAccumulator = 0f;

            foreach (var plot in _plots)
            {
                plot.Tick();
            }
        }

        private void BuildGrid()
        {
            var data = GameManager.Instance != null ? GameManager.Instance.playerData : null;
            for (int i = 0; i < gridSize; i++)
            {
                bool unlocked = data != null ? EconomyService.IsPlotUnlocked(data, i) : i < initiallyUnlocked;
                var slot = new PlotSlot(i, unlocked);
                if (data != null) slot.RestoreFrom(data.plots.Find(p => p.slotIndex == i));
                slot.Tick(); // одразу дорахувати час, що минув, поки гра була закрита
                slot.StateChanged += PersistSlot;
                _plots.Add(slot);

                if (plotViewPrefab == null || gridParent == null) continue;

                var view = Instantiate(plotViewPrefab, gridParent);
                view.Bind(slot, this);
                _views.Add(view);
            }
        }

        public int ReadyCount
        {
            get
            {
                int n = 0;
                foreach (var plot in _plots) if (plot.state == PlotState.Ready) n++;
                return n;
            }
        }

        /// <summary>Збирає всі дозрілі рослини одним тапом, з одним підсумковим тостом.</summary>
        public void HarvestAll()
        {
            int count = 0, coins = 0;
            foreach (var plot in _plots)
            {
                if (plot.state != PlotState.Ready) continue;
                coins += plot.Harvest(silent: true);
                count++;
            }
            if (count > 0) GameEvents.RaiseToast($"Зібрано {count} {PlantsWord(count)}: +{coins} монет");
        }

        private static string PlantsWord(int n)
        {
            int mod100 = n % 100, mod10 = n % 10;
            if (mod100 >= 11 && mod100 <= 14) return "рослин";
            if (mod10 == 1) return "рослину";
            if (mod10 >= 2 && mod10 <= 4) return "рослини";
            return "рослин";
        }

        private void PersistSlot(PlotSlot slot)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var plots = gm.playerData.plots;
            int idx = plots.FindIndex(p => p.slotIndex == slot.slotIndex);
            var save = slot.ToSave();
            if (idx >= 0) plots[idx] = save;
            else plots.Add(save);
            SaveSystem.Save(gm.playerData);
        }

        public void OnPlotTapped(PlotSlot slot)
        {
            switch (slot.state)
            {
                case PlotState.Empty:
                    OpenPlantingScreen(slot);
                    break;

                case PlotState.Ready:
                    slot.Harvest();
                    break;

                case PlotState.NeedsWater:
                    slot.Water();
                    break;

                case PlotState.Sick:
                    SickPlotTapped?.Invoke(slot);
                    break;

                case PlotState.Growing:
                    // Поки немає екрану догляду (GDD 5.2) — вікно прискорення за кристали.
                    GrowingPlotTapped?.Invoke(slot);
                    break;

                case PlotState.Locked:
                    if (EconomyService.TryBuyPlot(slot.slotIndex)) slot.Unlock();
                    break;
            }
        }

        private void OpenPlantingScreen(PlotSlot slot)
        {
            PlantingRequest.Set(slot, this);
            SceneManager.LoadScene(PlantingSceneName, LoadSceneMode.Additive);
        }
    }
}
