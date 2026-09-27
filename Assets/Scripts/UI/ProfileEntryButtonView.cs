using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Кільце круглої кнопки-аватара на «Моєму саду» (точка входу в «Профіль»,
    /// ТЗ «Персонаж») — тонується кольором поточного рівня престижу.
    /// Той самий патерн, що HeaderView: підписка на GameEvents, Redraw() у Start().
    /// </summary>
    public class ProfileEntryButtonView : MonoBehaviour
    {
        public Image ringImage;

        private void OnEnable() => GameEvents.OnLevelUp += HandleLevelUp;
        private void OnDisable() => GameEvents.OnLevelUp -= HandleLevelUp;

        private void Start() => Redraw();

        private void HandleLevelUp(int newLevel) => Redraw();

        private void Redraw()
        {
            if (GameManager.Instance == null || ringImage == null) return;
            ringImage.color = TierColor(GameManager.Instance.GetPrestigeTier());
        }

        private static Color TierColor(string tier) => tier switch
        {
            "spark" => UIColors.Hex("#E4C77E"),
            "gold" => UIColors.Hex("#E4C77E"),
            "green" => UIColors.Green,
            _ => UIColors.Hex("#5C6653"),
        };
    }
}
