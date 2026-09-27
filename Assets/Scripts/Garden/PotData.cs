using UnityEngine;

namespace PlantBreeding.Garden
{
    public enum PotRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Опис одного горщика (каталог — ContentSeeder, таблиця — docs/ECONOMY.md).
    /// Кожен горщик, крім стартового, трохи прискорює ріст і дає ще одну
    /// перевагу: рідший полив, дорожчий продаж або стійкість до хвороб і
    /// шкідників. Купується за монети один раз і лишається назавжди;
    /// ефекти фіксуються в момент посадки.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPot", menuName = "PlantBreeding/Pot Data")]
    public class PotData : ScriptableObject
    {
        [Header("Ідентифікація")]
        public string potId;
        public string displayName;
        public PotRarity rarity = PotRarity.Common;

        [Header("Ефекти (застосовуються при посадці)")]
        [Tooltip("Модифікатор часу росту, напр. -0.05 = -5% часу (швидше)")]
        public float growTimeModifier = 0f;
        [Tooltip("Бонус до ціни продажу врожаю, напр. 0.05 = +5%")]
        public float sellPriceBonus = 0f;
        [Tooltip("Рослина рідше просить води, напр. 0.5 = інтервал поливу +50%")]
        public float waterIntervalBonus = 0f;
        [Tooltip("Зниження шансу хвороби/шкідника, напр. 0.25 = −25%")]
        public float ailmentResistance = 0f;
        [Tooltip("Короткий підпис ефекту для картки (2 рядки), напр. \"−5% часу\\nполив рідше\"")]
        public string effectLabel = "без бонусу";

        [Header("Візуал (генерується процедурно, поки нема арту)")]
        public Color colorTop = Color.white;
        public Color colorMid = Color.gray;
        public Color colorBottom = Color.black;
        public Color colorRim = Color.gray;
        public Color colorStroke = Color.black;
        [Tooltip("Легендарний горщик отримує золоту рамку/бейдж на картці")]
        public bool premiumVisual = false;

        [Header("Економіка")]
        [Tooltip("Ціна одноразового розблокування за монети (0 = стартовий, завжди безкоштовний). " +
                 "Після покупки горщик лишається в гравця назавжди.")]
        public int unlockCost = 0;
    }
}
