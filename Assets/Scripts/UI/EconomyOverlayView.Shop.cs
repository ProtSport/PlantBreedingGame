using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Economy;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Крамниця кристалів — єдине джерело кристалів після стартового подарунка.
    /// Відкривається тапом на пілюлю кристалів у шапці будь-якого екрану
    /// (HeaderView), пунктом «Крамниця» нижнього меню саду або з вікна
    /// прискорення, коли кристалів бракує.
    /// Сама покупка — GemStore (поки без реальної оплати).
    /// </summary>
    public partial class EconomyOverlayView
    {
        private GameObject _shopModal;
        private TMP_Text _shopBalance;

        private void BuildShopModal()
        {
            _shopModal = MakeModal("ShopModal", CloseShop, out var panel);

            var title = MakeLabel(panel, "Title", "Крамниця", fontHead, 28, UIColors.Text, FontStyles.Normal);
            SetHeight(title.gameObject, 32);

            var sub = MakeLabel(panel, "Sub", "Кристали прискорюють ріст рослини в 10 разів", fontUi, 12, UIColors.Soft, FontStyles.Normal);
            sub.textWrappingMode = TextWrappingModes.Normal;
            SetHeight(sub.gameObject, 16);

            _shopBalance = MakeLabel(panel, "Balance", "", fontUi, 12, UIColors.Blue, FontStyles.Bold);
            SetHeight(_shopBalance.gameObject, 16);

            foreach (var pack in EconomyConfig.GemPacks) BuildPackRow(panel, pack);

            if (GemStore.IsTestMode)
            {
                var note = MakeLabel(panel, "TestNote",
                    "Тестовий режим: оплата ще не підключена, кристали нараховуються одразу.",
                    fontUi, 10, UIColors.GoldLt, FontStyles.Normal);
                note.textWrappingMode = TextWrappingModes.Normal;
                SetHeight(note.gameObject, 28);
            }

            var close = MakeClaimButton(panel, "Закрити", CloseShop);
            SetHeight(close.button.gameObject, 40);
            close.bg.color = UIColors.Rgba(Color.white, 0.08f);
        }

        private void BuildPackRow(Transform parent, GemPack pack)
        {
            var row = MakeRow(parent, "Pack_" + pack.productId, 56, UIColors.Rgba(UIColors.Blue, 0.08f));

            var amount = MakeLabel(row, "Amount", $"{pack.gems} {GemsWord(pack.gems)}", fontHead, 22, UIColors.Text, FontStyles.Normal);
            Place(amount.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 4), new Vector2(170, 26));

            if (!string.IsNullOrEmpty(pack.badge))
            {
                var badge = MakeLabel(row, "Badge", pack.badge, fontUi, 10, UIColors.GoldLt, FontStyles.Bold);
                Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, -15), new Vector2(170, 14));
            }

            var buy = MakeClaimButton(row, pack.priceLabel, () => GemStore.Purchase(pack));
            Place((RectTransform)buy.button.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(104, 36));
            buy.bg.color = UIColors.Blue;
        }

        /// <summary>
        /// Робить пункт «Крамниця» нижнього меню клікабельним (у білдері він
        /// декоративний). Слухач — у рантаймі: AddListener з Editor-білдера не серіалізується.
        /// </summary>
        private void HookShopEntryPoints()
        {
            var targets = new List<RectTransform>();
            if (shopNavItem != null) targets.Add(shopNavItem);

            foreach (var target in targets)
            {
                var img = target.GetComponent<Image>();
                if (img == null)
                {
                    img = target.gameObject.AddComponent<Image>();
                    img.color = new Color(0, 0, 0, 0.001f); // прозора зона кліку
                }
                img.raycastTarget = true;

                var btn = target.GetComponent<Button>();
                if (btn == null) btn = target.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(OpenShop);
            }
        }

        public void OpenShop()
        {
            if (_shopModal == null) return;
            _shopModal.SetActive(true);
            _shopModal.transform.SetAsLastSibling(); // поверх вікна прискорення
            RefreshShop();
        }

        private void CloseShop()
        {
            if (_shopModal != null) _shopModal.SetActive(false);
            RefreshSpeedUp();
        }

        private void RefreshShop()
        {
            var gm = GameManager.Instance;
            if (_shopBalance != null && gm != null)
                _shopBalance.text = $"У тебе: {gm.playerData.gems} {GemsWord(gm.playerData.gems)}";
            RefreshSpeedUp();
        }
    }
}
