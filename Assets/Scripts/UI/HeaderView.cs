using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Economy;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Єдина шапка всіх екранів (Сад, Посадка, Лабораторія, Дендрарій, Профіль):
    /// [назад] [★ завдання] [⚙ налаштування] … [монети] [кристали] [аватар] + рядок «РІВЕНЬ N · xp/next XP».
    /// Будує її UIBuilderKit.BuildStandardHeader (Editor), цей компонент оживляє:
    /// числа, бейдж незабраних нагород, тапи. Завдання й Крамниця відкриваються
    /// в EconomyOverlayView сцени саду — вона завантажена завжди (інші екрани
    /// довантажуються поверх неї), і її вікна мають вищий sortingOrder.
    /// Аватар відкриває Профіль (SceneNavButton на кнопці).
    /// </summary>
    public class HeaderView : MonoBehaviour
    {
        public TMP_Text coinsLabel;
        public TMP_Text gemsLabel;
        public TMP_Text levelLabel;
        public Button tasksButton;
        public GameObject tasksBadge;
        public TMP_Text tasksBadgeLabel;
        [Tooltip("Пілюля кристалів — тап відкриває Крамницю")]
        public Button gemsButton;
        [Tooltip("Шестірня — нижній лист «Налаштування» (тема, мова)")]
        public Button settingsButton;

        private void OnEnable()
        {
            GameEvents.OnCurrencyChanged += Redraw;
            GameEvents.OnLevelUp += HandleLevelUp;
            GameEvents.OnDailyStateChanged += Redraw;
        }

        private void OnDisable()
        {
            GameEvents.OnCurrencyChanged -= Redraw;
            GameEvents.OnLevelUp -= HandleLevelUp;
            GameEvents.OnDailyStateChanged -= Redraw;
        }

        // Слухачі — у Start (AddListener з Editor-білдера не серіалізується).
        private void Start()
        {
            if (tasksButton != null) tasksButton.onClick.AddListener(() => EconomyOverlayView.Instance?.OpenTasks());
            if (gemsButton != null) gemsButton.onClick.AddListener(() => EconomyOverlayView.Instance?.OpenShop());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => EconomyOverlayView.Instance?.OpenSettings());
            Redraw();
        }

        private void HandleLevelUp(int level) => Redraw();

        private void Redraw()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var data = gm.playerData;

            if (coinsLabel != null) coinsLabel.text = data.coins.ToString();
            if (gemsLabel != null) gemsLabel.text = data.gems.ToString();
            if (levelLabel != null)
                levelLabel.text = $"РІВЕНЬ {data.level} · {data.xp}/{EconomyService.XpForNextLevel(data.level)} XP";

            int claimable = EconomyService.ClaimableCount(data);
            if (tasksBadge != null) tasksBadge.SetActive(claimable > 0);
            if (tasksBadgeLabel != null) tasksBadgeLabel.text = claimable.ToString();
        }
    }
}
