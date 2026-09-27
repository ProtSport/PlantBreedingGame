using UnityEngine;
using PlantBreeding.Core;
using PlantBreeding.Save;

namespace PlantBreeding.Economy
{
    /// <summary>
    /// Покупка кристалів — ЄДИНЕ джерело кристалів після стартового подарунка
    /// (docs/ECONOMY.md). Реальна оплата (Unity IAP → Google Play Billing /
    /// StoreKit) ще не підключена: у редакторі й development-збірках покупка
    /// тестова (кристали нараховуються одразу, щоб перевіряти прискорення),
    /// у релізній збірці — лише повідомлення, що оплата незабаром.
    /// Коли підключимо IAP, змінюється тільки Purchase(): UI і EconomyConfig.GemPacks лишаються.
    /// </summary>
    public static class GemStore
    {
        public static bool IsTestMode => Application.isEditor || Debug.isDebugBuild;

        public static void Purchase(GemPack pack)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (!IsTestMode)
            {
                GameEvents.RaiseToast("Оплата ще не підключена — Крамниця запрацює незабаром");
                return;
            }

            gm.AddGems(pack.gems);
            SaveSystem.Save(gm.playerData);
            GameEvents.RaiseToast($"+{pack.gems} кристалів (тестова покупка, оплата не підключена)");
        }
    }
}
