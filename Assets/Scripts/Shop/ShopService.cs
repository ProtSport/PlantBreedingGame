using System;
using System.Collections.Generic;
using System.Linq;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;
using PlantBreeding.Save;

namespace PlantBreeding.Shop
{
    /// <summary>
    /// Ігрова логіка Крамниці (без оплати): що зараз можна купити і що
    /// нарахувати після успішної оплати. Саму оплату веде StoreGateway
    /// (In-App Purchase) і після підтвердження магазином викликає Grant.
    /// Стан — PlayerData (purchasedProductIds, clubActive, …).
    /// </summary>
    public static class ShopService
    {
        /// <summary>Щось куплено/змінився стан Клубу — перемалювати Крамницю, сад (нові грядки), Посадку.</summary>
        public static event Action Changed;

        private static GameManager Gm => GameManager.Instance;

        /// <summary>Для покупок поза магазином застосунків (SeedCapsule за кристали).</summary>
        internal static void NotifyChanged() => Changed?.Invoke();

        // ════════════════════════════════════════════════════════════════
        //  ДОСТУПНІСТЬ
        // ════════════════════════════════════════════════════════════════
        public static bool IsPurchased(PlayerData data, string productId) => data.purchasedProductIds.Contains(productId);

        public static bool IsOwned(PlayerData data, ShopProduct p) =>
            (p.type == ShopProductType.NonConsumable || p.oneTime) && IsPurchased(data, p.id);

        /// <summary>Чи показувати кнопку покупки (а не «Куплено»/замок/таймер, що минув).</summary>
        public static bool CanBuy(PlayerData data, ShopProduct p)
        {
            if (IsOwned(data, p)) return false;
            if (p.id == ShopCatalog.StarterId && StarterSecondsLeft(data) <= 0) return false;
            if (!string.IsNullOrEmpty(p.requiresId) && !IsPurchased(data, p.requiresId)) return false;
            if (p.club && IsClubActive(data)) return false;
            if (p.seeds > 0 && SeedPlant(data) == null) return false;
            // Горщик назавжди, який уже є (напр. з набору), — купувати вдруге нема сенсу.
            if (p.type == ShopProductType.NonConsumable && !string.IsNullOrEmpty(p.potId) && data.ownedPotIds.Contains(p.potId)) return false;
            return true;
        }

        public static double StarterSecondsLeft(PlayerData data)
        {
            if (data.firstLaunchUnixSeconds <= 0) return 0;
            double end = data.firstLaunchUnixSeconds + ShopCatalog.StarterWindowDays * 86400.0;
            return end - GameClock.NowUnixSeconds;
        }

        /// <summary>Найдорожчий вид, уже доступний за рівнем, — його насіння в «Наборі насіння».</summary>
        public static PlantData SeedPlant(PlayerData data) =>
            PlantCatalog.All.Where(p => p.unlockLevel <= data.level && p.seedCost > 0)
                .OrderByDescending(p => p.seedCost).FirstOrDefault();

        /// <summary>Скільки додаткових грядок (7-ма, 8-ма) куплено.</summary>
        public static int ExtraPlotCount(PlayerData data) =>
            ShopCatalog.All.Count(p => p.plotIndex >= 0 && IsPurchased(data, p.id));

        public static bool IsExtraPlotUnlocked(PlayerData data, int slotIndex) =>
            ShopCatalog.All.Any(p => p.plotIndex == slotIndex && IsPurchased(data, p.id));

        // ════════════════════════════════════════════════════════════════
        //  НАРАХУВАННЯ
        // ════════════════════════════════════════════════════════════════
        /// <summary>
        /// Видає товар після підтвердження оплати магазином. restored — повернення
        /// раніше купленого (Відновити покупки): без тостів про покупку.
        /// Повертає опис нарахованого для шторки «Готово».
        /// </summary>
        public static string Grant(ShopProduct p, bool restored = false)
        {
            var gm = Gm;
            if (gm == null || p == null) return "";
            var data = gm.playerData;
            var parts = new List<string>();

            if (restored && IsOwned(data, p)) return "";

            if (p.gems > 0) { gm.AddGems(p.gems); parts.Add($"+{p.gems} кристалів"); }
            if (p.coins > 0) { gm.AddCoins(p.coins); parts.Add($"+{p.coins} монет"); }
            if (!string.IsNullOrEmpty(p.potId) && !data.ownedPotIds.Contains(p.potId))
            {
                data.ownedPotIds.Add(p.potId);
                parts.Add($"горщик «{PotName(p.potId)}»");
            }
            if (p.seeds > 0)
            {
                var plant = SeedPlant(data);
                if (plant != null)
                {
                    gm.AddSeeds(plant.plantId, p.seeds);
                    parts.Add($"+{p.seeds} насіння «{plant.displayName}»");
                }
            }
            if (p.plotIndex >= 0) parts.Add($"{p.plotIndex + 1}-ма грядка в саду");
            if (p.club) parts.Add("Клуб садівника");

            if ((p.type == ShopProductType.NonConsumable || p.oneTime) && !data.purchasedProductIds.Contains(p.id))
                data.purchasedProductIds.Add(p.id);

            if (p.club)
            {
                data.clubActive = true;
                if (StoreGateway.IsTestStore)
                    data.clubUntilUnixSeconds = GameClock.NowUnixSeconds + 30 * 86400.0;
                GrantClubCosmetics(data);
                GrantClubDaily();
            }

            SaveSystem.Save(data);
            Changed?.Invoke();
            return string.Join(" · ", parts);
        }

        private static string PotName(string potId)
        {
            var pot = UnityEngine.Resources.Load<PotData>("Pots/" + potId);
            return pot != null ? pot.displayName : potId;
        }

        // ════════════════════════════════════════════════════════════════
        //  КЛУБ САДІВНИКА (підписка)
        // ════════════════════════════════════════════════════════════════
        public static bool IsClubActive(PlayerData data) =>
            data.clubActive || data.clubUntilUnixSeconds > GameClock.NowUnixSeconds;

        public static bool IsClubActive() => Gm != null && IsClubActive(Gm.playerData);

        /// <summary>Бонус до ціни продажу, поки активна підписка.</summary>
        public static float ClubSellBonus => IsClubActive() ? ShopCatalog.ClubSellBonus : 0f;

        /// <summary>Результат перевірки підписки магазином (StoreGateway).</summary>
        public static void SetClubEntitled(bool entitled)
        {
            var gm = Gm;
            if (gm == null) return;
            var data = gm.playerData;
            if (data.clubActive == entitled) return;
            data.clubActive = entitled;
            if (entitled) GrantClubCosmetics(data);
            if (entitled) GrantClubDaily();
            SaveSystem.Save(data);
            Changed?.Invoke();
        }

        /// <summary>Кристали й монети раз на ігровий день, поки активний Клуб (EconomyService.RollDay).</summary>
        public static void GrantClubDaily()
        {
            var gm = Gm;
            if (gm == null) return;
            var data = gm.playerData;
            if (!IsClubActive(data) || data.lastClubGemsDay == EconomyService.Today) return;
            GrantClubCosmetics(data);
            data.lastClubGemsDay = EconomyService.Today;
            gm.AddGems(ShopCatalog.ClubDailyGems);
            gm.AddCoins(ShopCatalog.ClubDailyCoins);
            SaveSystem.Save(data);
            GameEvents.RaiseToast($"Клуб садівника: +{ShopCatalog.ClubDailyGems} кристали і +{ShopCatalog.ClubDailyCoins} монет сьогодні");
        }

        /// <summary>Рамка «Клуб» і аватар «Сонце» — видаються при першій покупці й лишаються назавжди, навіть після скасування.</summary>
        private static void GrantClubCosmetics(PlayerData data)
        {
            if (!IsClubActive(data)) return;
            if (!data.ownedFrameIds.Contains(ShopCatalog.ClubFrameId)) data.ownedFrameIds.Add(ShopCatalog.ClubFrameId);
            if (!data.ownedAvatarIds.Contains(ShopCatalog.ClubAvatarId)) data.ownedAvatarIds.Add(ShopCatalog.ClubAvatarId);
        }

        // ════════════════════════════════════════════════════════════════
        //  ЗАХИСТ ВІД ПОДВІЙНОГО НАРАХУВАННЯ
        // ════════════════════════════════════════════════════════════════
        private const int MaxRememberedTransactions = 100;

        public static bool IsTransactionProcessed(string transactionId) =>
            !string.IsNullOrEmpty(transactionId) && Gm != null && Gm.playerData.processedTransactionIds.Contains(transactionId);

        public static void MarkTransactionProcessed(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId) || Gm == null) return;
            var list = Gm.playerData.processedTransactionIds;
            if (list.Contains(transactionId)) return;
            list.Add(transactionId);
            if (list.Count > MaxRememberedTransactions) list.RemoveAt(0);
        }
    }
}
