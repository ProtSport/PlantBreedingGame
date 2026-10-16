using UnityEngine;
using UnityEngine.SceneManagement;
using PlantBreeding.Core;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Кнопка «Зібрати все · N» у шапці саду — на місці лічильника «ростуть / готові»
    /// (праворуч від «Мій сад»; між сіткою з 8 грядок і меню вільного місця немає). З'являється, коли
    /// дозріло 2+ рослини (одну швидше зібрати тапом по грядці) і відкритий
    /// сам сад: оверлей малюється над усіма сценами, тож поверх Посадки,
    /// Догляду чи вкладок кнопку ховаємо.
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
            Place((RectTransform)_harvestAll.button.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-14, -116), new Vector2(160, 48));
            _harvestAll.button.transform.SetAsFirstSibling(); // під модалками й тостами
            GameEvents.OnPlotStateChanged += HandlePlotStateChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDestroy()
        {
            GameEvents.OnPlotStateChanged -= HandlePlotStateChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshHarvestAll();
        private void HandleSceneUnloaded(Scene scene) => RefreshHarvestAll();

        /// <summary>Відкритий лише сад — жодної додаткової сцени (Посадка, Догляд, вкладки, Профіль).</summary>
        private static bool OnlyGardenOpen()
        {
            int loaded = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isLoaded) loaded++;
            return loaded <= 1;
        }

        private void HandlePlotStateChanged(int slotIndex) => RefreshHarvestAll();

        private void RefreshHarvestAll()
        {
            if (_harvestAll == null) return;
            int ready = garden != null ? garden.ReadyCount : 0;
            bool show = ready >= HarvestAllMinReady && OnlyGardenOpen();
            _harvestAll.button.gameObject.SetActive(show);
            if (!show) return;
            SetClaim(_harvestAll, true, $"Зібрати все · {ready}");
            PositionHarvestAll();
        }

        /// <summary>Нижче вирізу камери — оверлей SafeAreaFitter не зсуває, тож рахуємо самі.</summary>
        private void PositionHarvestAll()
        {
            if (_harvestAll == null || !_harvestAll.button.gameObject.activeSelf) return;
            float inset = SafeAreaFitter.TopInsetFraction * _overlayRoot.rect.height;
            ((RectTransform)_harvestAll.button.transform).anchoredPosition = new Vector2(-14, -116 - inset);
        }
    }
}
