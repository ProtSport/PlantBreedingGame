using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using PlantBreeding.Core;
using PlantBreeding.Save;

namespace PlantBreeding.Shop
{
    public enum PurchaseOutcome
    {
        Success,
        Cancelled,
        Deferred, // очікує схвалення (напр. «Попросити купити» в Сімейному доступі)
        Failed
    }

    /// <summary>
    /// Оплата Крамниці за реальні гроші. На пристрої — Unity IAP 5
    /// (StoreKit на iOS, Google Play Billing на Android); Apple Pay для
    /// цифрових товарів у застосунку Apple не дозволяє (App Review 3.1.1),
    /// тому покупка йде через системне вікно In-App Purchase.
    /// У редакторі — тестовий магазин: покупка одразу успішна, щоб перевіряти
    /// нарахування без акаунтів App Store / Google Play.
    ///
    /// Нарахування — завжди ShopService.Grant ДО підтвердження транзакції
    /// магазину (ConfirmPurchase): якщо гра впаде посередині, магазин
    /// поверне незавершену покупку при наступному запуску.
    /// </summary>
    public static class StoreGateway
    {
        private static IStoreBackend _backend;

        /// <summary>Завантажились локалізовані ціни або змінився стан магазину.</summary>
        public static event Action StoreUpdated;

        public static bool IsTestStore => _backend is TestStoreBackend;
        public static bool IsReady => _backend != null && _backend.IsReady;

        public static void Init()
        {
            if (_backend != null) return;
            _backend = Application.isEditor ? new TestStoreBackend() : new IapStoreBackend();
            _backend.Init();
        }

        public static string PriceLabel(ShopProduct p)
        {
            string price = _backend?.LocalizedPrice(p);
            return string.IsNullOrEmpty(price) ? p.fallbackPrice : price;
        }

        /// <summary>done(outcome, повідомлення або опис нарахованого).</summary>
        public static void Purchase(ShopProduct p, Action<PurchaseOutcome, string> done)
        {
            if (_backend == null) Init();
            _backend.Purchase(p, done);
        }

        public static void Restore(Action<bool, string> done)
        {
            if (_backend == null) Init();
            _backend.Restore(done);
        }

        internal static void RaiseUpdated() => StoreUpdated?.Invoke();
    }

    internal interface IStoreBackend
    {
        bool IsReady { get; }
        void Init();
        string LocalizedPrice(ShopProduct p);
        void Purchase(ShopProduct p, Action<PurchaseOutcome, string> done);
        void Restore(Action<bool, string> done);
    }

    /// <summary>Редактор: покупка одразу успішна (оплати немає).</summary>
    internal class TestStoreBackend : IStoreBackend
    {
        public bool IsReady => true;
        public void Init() { }
        public string LocalizedPrice(ShopProduct p) => null;

        public void Purchase(ShopProduct p, Action<PurchaseOutcome, string> done)
        {
            string granted = ShopService.Grant(p);
            Debug.Log($"[Shop] Тестова покупка «{p.title}» ({p.StoreId}): {granted}");
            done?.Invoke(PurchaseOutcome.Success, granted);
        }

        public void Restore(Action<bool, string> done) =>
            done?.Invoke(true, "Тестовий магазин: покупки вже збережені в грі");
    }

    /// <summary>Пристрій: Unity IAP 5 (StoreController).</summary>
    internal class IapStoreBackend : IStoreBackend
    {
        private StoreController _store;
        private bool _productsFetched;
        private readonly Dictionary<string, Action<PurchaseOutcome, string>> _callbacks =
            new Dictionary<string, Action<PurchaseOutcome, string>>();
        private Action<bool, string> _restoreCallback;

        public bool IsReady => _productsFetched;

        public async void Init()
        {
            try
            {
                _store = UnityIAPServices.StoreController();
                _store.OnPurchasePending += OnPurchasePending;
                _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _store.OnPurchaseFailed += OnPurchaseFailed;
                _store.OnPurchaseDeferred += OnPurchaseDeferred;
                _store.OnProductsFetched += OnProductsFetched;
                _store.OnProductsFetchFailed += f =>
                    Debug.LogWarning($"[Shop] Не вдалося завантажити товари: {f.FailureReason}");
                _store.OnPurchasesFetched += OnPurchasesFetched;
                _store.OnPurchasesFetchFailed += f =>
                    Debug.LogWarning($"[Shop] Не вдалося отримати покупки: {f.message}");
                _store.OnCheckEntitlement += OnCheckEntitlement;
                _store.OnStoreDisconnected += d => Debug.LogWarning($"[Shop] Магазин від'єднано: {d.message}");
                // Незавершені покупки (гра закрилась під час оплати) приходять у OnPurchasePending.
                _store.ProcessPendingOrdersOnPurchasesFetched(true);

                await _store.Connect();

                var definitions = ShopCatalog.All
                    .Select(p => new ProductDefinition(p.id, p.StoreId, ToIapType(p.type)))
                    .ToList();
                _store.FetchProducts(definitions);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Shop] Магазин недоступний: {e.Message}");
            }
        }

        private static ProductType ToIapType(ShopProductType t) => t switch
        {
            ShopProductType.NonConsumable => ProductType.NonConsumable,
            ShopProductType.Subscription => ProductType.Subscription,
            _ => ProductType.Consumable,
        };

        public string LocalizedPrice(ShopProduct p)
        {
            if (!_productsFetched || _store == null) return null;
            var product = _store.GetProductById(p.id);
            return product?.metadata?.localizedPriceString;
        }

        public void Purchase(ShopProduct p, Action<PurchaseOutcome, string> done)
        {
            var product = _productsFetched ? _store.GetProductById(p.id) : null;
            if (product == null || !product.availableToPurchase)
            {
                done?.Invoke(PurchaseOutcome.Failed, "Магазин недоступний. Перевір інтернет і спробуй пізніше.");
                return;
            }
            _callbacks[p.id] = done;
            _store.PurchaseProduct(product);
        }

        public void Restore(Action<bool, string> done)
        {
            if (_store == null)
            {
                done?.Invoke(false, "Магазин недоступний. Перевір інтернет і спробуй пізніше.");
                return;
            }
            _restoreCallback = done;
            // Відновлені покупки приходять у OnPurchasesFetched / OnPurchasePending.
            _store.RestoreTransactions((ok, error) =>
            {
                if (ok) _store.FetchPurchases();
                var cb = _restoreCallback;
                _restoreCallback = null;
                cb?.Invoke(ok, ok ? "Покупки відновлено" : $"Не вдалося відновити: {error}");
            });
        }

        // ── Події магазину ───────────────────────────────────────────────
        private void OnProductsFetched(List<Product> products)
        {
            _productsFetched = true;
            Debug.Log($"[Shop] Завантажено товарів: {products.Count}");
            _store.FetchPurchases(); // куплене раніше (горщики, грядки, підписка)
            CheckClub();
            StoreGateway.RaiseUpdated();
        }

        private void OnPurchasePending(PendingOrder order)
        {
            var shopProduct = ShopProductOf(order);
            string txId = order.Info?.TransactionID;
            string granted = "";

            if (shopProduct != null && !ShopService.IsTransactionProcessed(txId))
            {
                granted = ShopService.Grant(shopProduct);
                ShopService.MarkTransactionProcessed(txId);
                SaveGame();
            }

            _store.ConfirmPurchase(order);
            if (shopProduct != null) Complete(shopProduct.id, PurchaseOutcome.Success, granted);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed) OnPurchaseFailed(failed);
            if (ShopProductOf(order)?.club == true) CheckClub();
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            var shopProduct = ShopProductOf(order);
            Debug.LogWarning($"[Shop] Покупка не вдалась: {shopProduct?.id} — {order.FailureReason} {order.Details}");
            if (shopProduct == null) return;
            bool cancelled = order.FailureReason == PurchaseFailureReason.UserCancelled;
            Complete(shopProduct.id, cancelled ? PurchaseOutcome.Cancelled : PurchaseOutcome.Failed,
                cancelled ? "" : "Оплата не пройшла. Гроші не списано.");
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            var shopProduct = ShopProductOf(order);
            if (shopProduct != null)
                Complete(shopProduct.id, PurchaseOutcome.Deferred, "Покупка чекає на схвалення. Товар з'явиться після підтвердження.");
        }

        /// <summary>Уже куплене на цьому акаунті: повертаємо горщики/грядки (напр. після перевстановлення).</summary>
        private void OnPurchasesFetched(Orders orders)
        {
            bool changed = false;
            foreach (var order in orders.ConfirmedOrders)
            {
                var p = ShopProductOf(order);
                if (p == null || p.type != ShopProductType.NonConsumable) continue;
                if (GameManager.Instance != null && !ShopService.IsOwned(GameManager.Instance.playerData, p))
                {
                    ShopService.Grant(p, restored: true);
                    changed = true;
                }
            }
            if (changed) SaveGame();
            CheckClub();
        }

        private void CheckClub()
        {
            var club = _store?.GetProductById(ShopCatalog.ClubId);
            if (club != null) _store.CheckEntitlement(club);
        }

        private void OnCheckEntitlement(Entitlement entitlement)
        {
            if (entitlement.Product?.definition?.id != ShopCatalog.ClubId) return;
            if (entitlement.Status == EntitlementStatus.Unknown) return; // офлайн — лишаємо останній відомий стан
            ShopService.SetClubEntitled(entitlement.Status == EntitlementStatus.FullyEntitled);
            StoreGateway.RaiseUpdated();
        }

        // ── Хелпери ──────────────────────────────────────────────────────
        private static ShopProduct ShopProductOf(Order order)
        {
            var item = order?.CartOrdered?.Items()?.FirstOrDefault();
            var id = item?.Product?.definition?.id;
            return id != null ? ShopCatalog.Get(id) : null;
        }

        private void Complete(string productId, PurchaseOutcome outcome, string message)
        {
            if (!_callbacks.TryGetValue(productId, out var cb)) return;
            _callbacks.Remove(productId);
            cb?.Invoke(outcome, message);
        }

        private static void SaveGame()
        {
            if (GameManager.Instance != null) SaveSystem.Save(GameManager.Instance.playerData);
        }
    }
}
