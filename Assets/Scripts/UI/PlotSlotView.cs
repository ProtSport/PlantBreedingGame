using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Візуальна клітинка грядки на екрані саду. Суто "вьюха" — жодної
    /// ігрової логіки, тільки відображення стану PlotSlot і передача тапів
    /// назад у GardenManager. Будується процедурно в Assets/Editor/HomeScreenBuilder.cs
    /// (усі поля нижче заповнюються там же при генерації сцени).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PlotSlotView : MonoBehaviour
    {
        [Header("Групи станів")]
        public GameObject contentGroup;  // рослина + чіп + назва + прогрес (Growing/NeedsWater/Sick/Ready)
        public GameObject emptyGroup;    // "+" Посадити (Empty)
        public GameObject lockedGroup;   // 🔒 Заблоковано (Locked)

        [Header("Фон/рамка тайла")]
        public Image background;
        public Image border;
        public Image readyGlow;

        [Header("Контент")]
        public Image plantImage;
        public Image chipBg;
        public Image chipBorder;
        public Image chipDot;
        public Image chipWarningIcon;
        public TMP_Text chipText;
        public TMP_Text nameLabel;
        public Image progressFill;

        [Header("Хвороби/шкідники (арт — Assets/Art/Ailments)")]
        [Tooltip("Дочірній до plantImage, з Mask: спрайт «тільки рослина» обрізає оверлей по силуету")]
        public Image ailmentMask;
        public Image ailmentOverlay;
        [Tooltip("Павутина кліща — без маски, поверх рослини")]
        public Image webOverlay;
        public Sprite sprAphids;
        public Sprite sprPowderyMildew;
        public Sprite sprChlorosis;

        [Header("Заблокована грядка")]
        public TMP_Text lockedLabel; // "Рівень 2 / або 100 монет" (EconomyService.LockedPlotLabel)

        private PlotSlot _slot;
        private GardenManager _manager;
        private Button _button;
        private int _lastGrowthBucket = -1;
        private float _chipTimer;

        public void Bind(PlotSlot slot, GardenManager manager)
        {
            _slot = slot;
            _manager = manager;

            _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleTap);

            _slot.StateChanged += _ => Redraw();
            Redraw();
        }

        private void Update()
        {
            // Прогрес оновлюємо щокадру для плавної анімації, решту — лише по StateChanged.
            if (_slot == null || progressFill == null) return;
            if (_slot.state == PlotState.Growing || _slot.state == PlotState.NeedsWater || _slot.state == PlotState.Sick)
            {
                float progress = _slot.GetGrowthProgress01();
                SetProgress(progress);
                RefreshGrowthSprite(progress);

                // Таймер у чіпі — раз на секунду, не щокадру.
                _chipTimer -= Time.deltaTime;
                if (_chipTimer <= 0f && _slot.state == PlotState.Growing && chipText != null)
                {
                    _chipTimer = 1f;
                    chipText.text = FormatRemaining(_slot.GetRemainingSeconds());
                }
            }
        }

        /// <summary>"2 год 15 хв" / "12 хв" / "40 с" — коли повертатися по урожай.</summary>
        public static string FormatRemaining(double seconds) => TimeFormat.Remaining(seconds);

        private void HandleTap()
        {
            _manager.OnPlotTapped(_slot);
        }

        private void Redraw()
        {
            bool isContent = _slot.state == PlotState.Growing || _slot.state == PlotState.NeedsWater
                              || _slot.state == PlotState.Sick || _slot.state == PlotState.Ready;

            if (contentGroup != null) contentGroup.SetActive(isContent);
            if (emptyGroup != null) emptyGroup.SetActive(_slot.state == PlotState.Empty);
            if (lockedGroup != null) lockedGroup.SetActive(_slot.state == PlotState.Locked);
            if (readyGlow != null) readyGlow.gameObject.SetActive(_slot.state == PlotState.Ready);

            if (_slot.state == PlotState.Locked)
            {
                var label = lockedLabel != null ? lockedLabel
                    : lockedGroup != null ? lockedGroup.GetComponentInChildren<TMP_Text>(true) : null;
                if (label != null) label.text = EconomyService.LockedPlotLabel(_slot.slotIndex);
            }

            if (!isContent)
            {
                if (ailmentMask != null) ailmentMask.gameObject.SetActive(false);
                if (webOverlay != null) webOverlay.gameObject.SetActive(false);
                return;
            }

            if (plantImage != null && _slot.plant != null)
            {
                plantImage.enabled = true;
                _lastGrowthBucket = -1; // форсувати перерахунок спрайту нижче (нова рослина/стан)
                RefreshGrowthSprite(_slot.state == PlotState.Ready ? 1f : _slot.GetGrowthProgress01());
                plantImage.color = _slot.state == PlotState.Sick ? new Color(0.85f, 0.83f, 0.80f) : _slot.plant.tint;
            }

            if (nameLabel != null) nameLabel.text = _slot.plant != null ? _slot.plant.displayName : "";
            RefreshAilmentOverlay();

            ApplyChipStyle();
            SetProgress(_slot.GetGrowthProgress01());
        }

        /// <summary>
        /// Імітація росту: 4 порогові картинки (25/50/75/100%) замість однієї
        /// статичної. Міняє sprite лише коли перетнуто поріг (не щокадру),
        /// щоб не смикати Image даремно.
        /// </summary>
        private void RefreshGrowthSprite(float progress01)
        {
            if (plantImage == null || _slot.plant == null) return;
            int bucket = progress01 < 0.25f ? 0 : progress01 < 0.50f ? 1 : progress01 < 0.75f ? 2 : 3;
            if (bucket == _lastGrowthBucket) return;
            _lastGrowthBucket = bucket;

            var sprite = _slot.plant.GetGrowthSprite(progress01);
            if (sprite != null) plantImage.sprite = sprite;
            var maskSprite = _slot.plant.GetPlantOnlySprite(progress01);
            if (ailmentMask != null && maskSprite != null) ailmentMask.sprite = maskSprite;
        }

        /// <summary>
        /// Оверлей хвороби за README арту: тля/роса/хлороз — по масці силуету
        /// рослини; кліщ — павутина без маски. Шкідник блідне з кожним тапом
        /// (opacity = 0.3 + 0.7 · залишок/всього). Хлороз у uGUI без режиму
        /// змішування «Color», тому напівпрозорий.
        /// </summary>
        private void RefreshAilmentOverlay()
        {
            bool sick = _slot.state == PlotState.Sick && _slot.plant != null;
            var kind = sick ? _slot.ailment : AilmentKind.None;
            bool hasMask = sick && _slot.plant.GetPlantOnlySprite(1f) != null;

            Sprite masked = kind switch
            {
                AilmentKind.Aphids => sprAphids,
                AilmentKind.PowderyMildew => sprPowderyMildew,
                AilmentKind.Chlorosis => sprChlorosis,
                _ => null
            };
            bool showMasked = hasMask && masked != null;
            if (ailmentMask != null) ailmentMask.gameObject.SetActive(showMasked);
            if (ailmentOverlay != null && showMasked)
            {
                ailmentOverlay.sprite = masked;
                float a = kind == AilmentKind.Chlorosis ? 0.6f : 1f;
                if (kind == AilmentKind.Aphids) a = PestAlpha();
                ailmentOverlay.color = new Color(1f, 1f, 1f, a);
            }

            bool showWeb = sick && kind == AilmentKind.SpiderMite;
            if (webOverlay != null)
            {
                webOverlay.gameObject.SetActive(showWeb);
                if (showWeb) webOverlay.color = new Color(1f, 1f, 1f, PestAlpha());
            }
        }

        private float PestAlpha()
        {
            int total = PlantAilments.PestTaps(_slot.ailment);
            return total > 0 ? 0.3f + 0.7f * Mathf.Clamp01((float)_slot.pestTapsLeft / total) : 1f;
        }

        private void ApplyChipStyle()
        {
            Color bg, border_, text;
            string label;
            bool showWarning = false;

            switch (_slot.state)
            {
                case PlotState.Ready:
                    bg = new Color(0.655f, 0.808f, 0.451f, 0.22f);
                    border_ = new Color(0.655f, 0.808f, 0.451f, 0.40f);
                    text = new Color(0.804f, 0.922f, 0.627f);
                    label = "Готово";
                    break;
                case PlotState.Sick:
                    bg = new Color(0.839f, 0.471f, 0.337f, 0.20f);
                    border_ = new Color(0.839f, 0.471f, 0.337f, 0.40f);
                    text = new Color(0.929f, 0.725f, 0.541f);
                    // Назва хвороби/шкідника; для шкідника — скільки тапів лишилось.
                    var ailment = _slot.CurrentAilment;
                    label = ailment.isPest && _slot.pestTapsLeft > 0 ? $"{ailment.name} · {_slot.pestTapsLeft}" : ailment.name;
                    showWarning = true;
                    break;
                case PlotState.NeedsWater:
                    bg = new Color(1, 1, 1, 0.06f);
                    border_ = new Color(1, 1, 1, 0.12f);
                    text = new Color(0.776f, 0.816f, 0.910f);
                    label = "Полити";
                    break;
                default: // Growing
                    bg = new Color(1, 1, 1, 0.06f);
                    border_ = new Color(1, 1, 1, 0.12f);
                    text = new Color(0.776f, 0.816f, 0.910f);
                    label = FormatRemaining(_slot.GetRemainingSeconds());
                    break;
            }

            if (chipBg != null) chipBg.color = bg;
            if (chipBorder != null) chipBorder.color = border_;
            if (chipText != null) { chipText.text = label; chipText.color = text; }
            if (chipDot != null)
            {
                chipDot.gameObject.SetActive(!showWarning);
                chipDot.color = _slot.state == PlotState.Ready
                    ? new Color(0.710f, 0.902f, 0.478f)
                    : new Color(0.624f, 0.698f, 0.902f);
            }
            if (chipWarningIcon != null) chipWarningIcon.gameObject.SetActive(showWarning);
        }

        private void SetProgress(float progress01)
        {
            if (progressFill == null) return;
            var rt = progressFill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(progress01), rt.anchorMax.y);

            Color fillCol = _slot.state == PlotState.Sick
                ? new Color(0.906f, 0.651f, 0.420f)
                : (_slot.state == PlotState.Ready ? new Color(0.710f, 0.902f, 0.478f) : new Color(0.655f, 0.808f, 0.451f));
            progressFill.color = fillCol;
        }
    }
}
