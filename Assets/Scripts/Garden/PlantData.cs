using UnityEngine;

namespace PlantBreeding.Garden
{
    /// <summary>
    /// Опис одного виду/сорту рослини. Створюється як асет через
    /// Assets > Create > PlantBreeding > Plant Data.
    /// В майбутньому геном (кольори/розмір/аромат тощо, розділ 3 GDD)
    /// додасться сюди окремим полем GenomeDefinition — MVP поки що
    /// оперує готовим набором видів без генерації на льоту.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlant", menuName = "PlantBreeding/Plant Data")]
    public class PlantData : ScriptableObject
    {
        [Header("Ідентифікація")]
        public string plantId;         // унікальний id, напр. "violet"
        public string displayName;     // "Фіалка"

        [Header("Ріст")]
        [Tooltip("Час повного росту в секундах (реальний час)")]
        public float growTimeSeconds = 60f * 60f * 2f; // 2 години за замовчуванням

        [Tooltip("Як часто (в секундах) падає волога і треба поливати")]
        public float waterDepleteSeconds = 60f * 60f * 3f;

        [Header("Візуал (стадії росту — 4 картинки на 25/50/75/100% прогресу)")]
        [Tooltip("Рослина з горщиком і тінню (Assets/Art/Plants/<id>/stageN.svg): паросток → доросла")]
        public Sprite sprite25;
        public Sprite sprite50;
        public Sprite sprite75;
        public Sprite sprite100;

        [Header("Тільки рослина, без горщика (stageN-plant.svg)")]
        [Tooltip("Альфа — маска для оверлеїв хвороб; також для прев'ю над обраним горщиком. Те саме полотно 200×260.")]
        public Sprite plantOnly25;
        public Sprite plantOnly50;
        public Sprite plantOnly75;
        public Sprite plantOnly100;

        [Header("Рідкість")]
        public PlantRarity rarity = PlantRarity.Common;

        [Header("Економіка")]
        [Tooltip("Ціна поповнення насіння в екрані Посадки, коли запас = 0. " +
                 "0 = не продається за монети (рідкісні+ сорти здобуваються лише схрещуванням).")]
        public int seedCost = 25;

        [Tooltip("Ціна продажу зібраної рослини (до бонусів догляду/лабораторії). " +
                 "0 = брати базову ціну рідкості (PlantEconomy).")]
        public int sellPrice = 0;

        [Tooltip("XP за збір урожаю")]
        public int xpReward = 5;

        [Tooltip("Рівень гравця, з якого насіння з'являється в продажу")]
        public int unlockLevel = 1;

        [Header("Візуал-плейсхолдер")]
        [Tooltip("Тон, яким фарбуються спільні спрайти стадій, поки в кожного виду немає власного арту")]
        public Color tint = Color.white;

        /// <summary>Спрайт для поточного прогресу росту (0..1) — 4 порогові картинки, без проміжних станів.</summary>
        public Sprite GetGrowthSprite(float progress01)
        {
            if (progress01 < 0.25f) return sprite25;
            if (progress01 < 0.50f) return sprite50;
            if (progress01 < 0.75f) return sprite75;
            return sprite100;
        }

        /// <summary>Та сама стадія, але тільки рослина (маска хвороб); null, якщо арту ще нема.</summary>
        public Sprite GetPlantOnlySprite(float progress01)
        {
            if (progress01 < 0.25f) return plantOnly25;
            if (progress01 < 0.50f) return plantOnly50;
            if (progress01 < 0.75f) return plantOnly75;
            return plantOnly100;
        }
    }

    public enum PlantRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }
}
