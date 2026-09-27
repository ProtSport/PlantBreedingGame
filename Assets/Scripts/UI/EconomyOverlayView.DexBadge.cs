using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Кружечок з числом у правому верхньому куті іконки «Дендрарій» нижнього
    /// меню: скільки нових рослин потрапило в колекції (PlayerData.unseenDexCount).
    /// Зникає, коли гравець відкриває Дендрарій (DexScreenController → MarkDexSeen).
    /// </summary>
    public partial class EconomyOverlayView
    {
        [Tooltip("Пункт «Дендрарій» нижнього меню — на його іконку ставиться бейдж нових рослин")]
        public RectTransform dexNavItem;

        private GameObject _dexBadge;
        private TMP_Text _dexBadgeLabel;

        private void BuildDexBadge()
        {
            if (dexNavItem == null) return;
            // Кріпимо до IconBox (без лейауту) — позиція абсолютна відносно іконки.
            var iconBox = dexNavItem.Find("IconBox") as RectTransform;
            var parent = iconBox != null ? iconBox : dexNavItem;

            var badge = MakeImage(parent, "NewBadge", sprCircle, UIColors.Hex("#D9744F"), Image.Type.Simple);
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-4, -3), new Vector2(18, 18));

            var ring = MakeImage(badge.transform, "Ring", sprCircle, UIColors.Hex("#12180D"), Image.Type.Simple);
            Stretch(ring.rectTransform);
            ring.rectTransform.offsetMin = new Vector2(-2, -2);
            ring.rectTransform.offsetMax = new Vector2(2, 2);
            ring.transform.SetAsFirstSibling(); // темне кільце під кружечком — відділяє від іконки

            // Кільце — дочірній об'єкт, тож малюється поверх фону бейджа; кружечок
            // малюємо окремим шаром над кільцем.
            var dot = MakeImage(badge.transform, "Dot", sprCircle, UIColors.Hex("#D9744F"), Image.Type.Simple);
            Stretch(dot.rectTransform);
            badge.color = Color.clear;

            _dexBadgeLabel = MakeLabel(badge.transform, "Count", "", fontUi, 10, Color.white, FontStyles.Bold);
            Stretch(_dexBadgeLabel.rectTransform);
            _dexBadgeLabel.alignment = TextAlignmentOptions.Center;

            _dexBadge = badge.gameObject;
        }

        private void RefreshDexBadge()
        {
            if (_dexBadge == null) return;
            var gm = GameManager.Instance;
            int count = gm != null ? gm.playerData.unseenDexCount : 0;
            _dexBadge.SetActive(count > 0);
            if (count > 0) _dexBadgeLabel.text = count > 9 ? "9+" : count.ToString();
        }
    }
}
