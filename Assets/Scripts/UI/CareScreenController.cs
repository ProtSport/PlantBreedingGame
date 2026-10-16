using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlantBreeding.Collections;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;
using PlantBreeding.Shop;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Екран «Догляд» (RELEASE-PLAN п. 3) — окрема сцена «Care», відкривається з
    /// картки рослини (GardenManager.OpenCareScreen → CareRequest). Дві прості дії
    /// без міні-ігор:
    ///  • Полив — рослина хоче пити раз на ~чверть росту, кожен полив +10% до ціни (до 3);
    ///  • Світло — двічі за ріст «хоче на сонце», кожне +5% до ціни (PlotSlot.GiveSun).
    /// Догляд не зупиняє ріст — лише додає до ціни. Свайп (або стрілки) — сусідні
    /// рослини саду без повернення на головний екран; дозрілу можна зібрати тут же.
    ///
    /// Каркас (фон, шапка з «назад», заголовок, область Content) збирає
    /// Assets/Editor/CareScreenBuilder.cs, решту UI — цей скрипт у Start() один раз.
    /// </summary>
    public class CareScreenController : MonoBehaviour
    {
        [Header("Графіка")]
        public TMP_FontAsset fontHead;
        public TMP_FontAsset fontUi;
        public Sprite sprCard;
        public Sprite sprCardLine;
        public Sprite sprPill;
        public Sprite sprCircle;
        public Sprite sprGlow;
        public Sprite sprWater;
        public Sprite sprSun;

        [Header("Каркас сцени")]
        public Button backButton;
        public RectTransform content;

        private class ActionCard
        {
            public Button button;
            public Image border, iconBg, icon;
            public TMP_Text title, status, counter;
            public bool needed;
        }

        private readonly List<PlotSlot> _slots = new List<PlotSlot>();
        private PlotSlot _slot;

        private Image _plant, _glow;
        private TMP_Text _name, _sub, _remaining, _pager, _price, _hint;
        private RectTransform _fill;
        private GameObject _prev, _next;
        private ActionCard _water, _sun;
        private Button _harvest;
        private float _refreshTimer;

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(Close);
            BuildUi();
            _slot = CareRequest.TargetSlot;
            CollectSlots();
            if (_slot == null || !_slots.Contains(_slot)) _slot = _slots.Count > 0 ? _slots[0] : null;
            Refresh();
        }

        private void Update()
        {
            // Пульс іконки дії, на яку рослина чекає.
            float pulse = 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 5f);
            if (_water != null) _water.icon.rectTransform.localScale = Vector3.one * (_water.needed ? pulse : 1f);
            if (_sun != null) _sun.icon.rectTransform.localScale = Vector3.one * (_sun.needed ? pulse : 1f);

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.5f;
            Refresh();
        }

        private void Close()
        {
            CareRequest.Clear();
            SceneManager.UnloadSceneAsync(GardenManager.CareSceneName);
        }

        /// <summary>Рослини саду, які можна доглядати або зібрати, — для свайпу.</summary>
        private void CollectSlots()
        {
            _slots.Clear();
            var garden = CareRequest.SourceManager != null ? CareRequest.SourceManager : GardenManager.Current;
            if (garden == null) return;
            foreach (var s in garden.Plots)
                if (s.plant != null && (s.state == PlotState.Growing || s.state == PlotState.NeedsWater || s.state == PlotState.Ready))
                    _slots.Add(s);
        }

        private void Step(int dir)
        {
            CollectSlots();
            if (_slots.Count == 0) return;
            int i = _slots.IndexOf(_slot);
            i = i < 0 ? 0 : (i + dir + _slots.Count) % _slots.Count;
            _slot = _slots[i];
            Refresh();
        }

        // ════════════════════════════════════════════════════════════════
        //  ВІДМАЛЬОВКА
        // ════════════════════════════════════════════════════════════════
        private void Refresh()
        {
            CollectSlots();
            if (_slot == null || !_slots.Contains(_slot))
            {
                // Поточну зібрали (або сад порожній) — до наступної, а якщо нема — назад у сад.
                if (_slots.Count == 0) { Close(); enabled = false; return; }
                _slot = _slots[0];
            }

            var slot = _slot;
            var plant = slot.plant;
            bool ready = slot.state == PlotState.Ready;
            float progress = ready ? 1f : slot.GetGrowthProgress01();

            _plant.sprite = plant.GetGrowthSprite(progress);
            _plant.color = plant.tint;
            _glow.color = UIColors.Rgba(slot.WantsSun ? UIColors.GoldLt : UIColors.Green, slot.WantsSun ? 0.30f : 0.14f);
            _name.text = plant.displayName;
            _sub.text = slot.pot != null
                ? $"{SeedCapsule.RarityName(plant.rarity)} · {slot.pot.displayName}"
                : SeedCapsule.RarityName(plant.rarity);
            _remaining.text = ready ? "Дозріла!" : $"До врожаю: {TimeFormat.Remaining(slot.GetRemainingSeconds())}";
            _fill.anchorMax = new Vector2(progress, 1);

            bool many = _slots.Count > 1;
            _prev.SetActive(many);
            _next.SetActive(many);
            _pager.text = many ? $"{_slots.IndexOf(slot) + 1} / {_slots.Count}" : "";

            int basePrice = PlantingForecast.SellPriceWithBonuses(plant, slot.pot);
            int now = Mathf.RoundToInt(basePrice * EconomyService.CareMultiplier(slot.wateredCount, slot.sunCount));
            int best = Mathf.RoundToInt(basePrice * EconomyService.CareMultiplier(EconomyConfig.MaxCareWaterings, EconomyConfig.MaxSunnings));
            _price.text = now < best && !ready ? $"Ціна зараз: {now} монет · з доглядом до {best}" : $"Ціна: {now} монет";

            RefreshAction(_water, ready ? -1 : slot.SecondsUntilWater(), slot.wateredCount, EconomyConfig.MaxCareWaterings,
                Mathf.RoundToInt(CollectionService.CareBonusPerWatering * 100f), "Хоче пити!", UIColors.Blue);
            RefreshAction(_sun, ready ? -1 : slot.SecondsUntilSun(), slot.sunCount, EconomyConfig.MaxSunnings,
                Mathf.RoundToInt(EconomyConfig.SunBonus * 100f), "Хоче на сонце!", UIColors.GoldLt);

            _harvest.gameObject.SetActive(ready);
            _hint.gameObject.SetActive(!ready);
        }

        /// <param name="secondsUntil">0 — просить зараз, &gt;0 — через стільки, &lt;0 — більше не попросить.</param>
        private void RefreshAction(ActionCard card, double secondsUntil, int done, int max, int bonusPercent, string wantText, Color accent)
        {
            card.needed = secondsUntil == 0;
            card.button.interactable = card.needed;
            card.border.color = card.needed ? UIColors.Rgba(accent, 0.75f) : UIColors.Rgba(Color.white, 0.10f);
            card.iconBg.color = UIColors.Rgba(accent, card.needed ? 0.26f : 0.08f);
            card.icon.color = card.needed ? accent : UIColors.Rgba(accent, 0.45f);
            card.status.color = card.needed ? accent : UIColors.Soft;
            card.status.text = card.needed ? wantText
                : secondsUntil > 0 ? $"Знову через {TimeFormat.Remaining(secondsUntil)}"
                : done >= max ? "Досить на цей ріст" : "Не потрібно";
            card.counter.text = $"{done} з {max} · +{bonusPercent}% кожен";
        }

        private void OnWater()
        {
            if (_slot == null || _slot.state != PlotState.NeedsWater) return;
            _slot.Water();
            Refresh();
        }

        private void OnSun()
        {
            if (_slot == null || !_slot.WantsSun) return;
            _slot.GiveSun();
            Refresh();
        }

        private void OnHarvest()
        {
            if (_slot == null || _slot.state != PlotState.Ready) return;
            _slot.Harvest();
            Refresh();
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОБУДОВА (один раз)
        // ════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            // Низ екрана фіксованої висоти (підказка/«Зібрати», ціна, дві картки дій),
            // сцена з рослиною — усе, що лишилось: так на iPhone SE і на 21:9 нічого не налазить.
            // ── Сцена з рослиною — свайп між рослинами ──
            var stage = MakeImage(content, "Stage", sprCard, UIColors.Rgba(Color.white, 0.04f), Image.Type.Sliced);
            stage.raycastTarget = true; // ловить свайп
            Anchor(stage.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 218), new Vector2(-16, 0));
            var line = MakeImage(stage.transform, "Border", sprCardLine, UIColors.Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            stage.gameObject.AddComponent<SwipeArea>().Swiped += Step;

            _glow = MakeImage(stage.transform, "Glow", sprGlow, UIColors.Rgba(UIColors.Green, 0.14f), Image.Type.Simple);
            Anchor(_glow.rectTransform, new Vector2(0.1f, 0), new Vector2(0.9f, 1), new Vector2(0, 96), new Vector2(0, -4));

            _plant = MakeImage(stage.transform, "Plant", null, Color.white, Image.Type.Simple);
            _plant.preserveAspect = true;
            Anchor(_plant.rectTransform, new Vector2(0.15f, 0), new Vector2(0.85f, 1), new Vector2(0, 102), new Vector2(0, -10));

            _name = MakeLabel(stage.transform, "Name", "", fontHead, 30, UIColors.Text, FontStyles.Normal);
            Anchor(_name.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 62), new Vector2(-16, 98));
            _name.alignment = TextAlignmentOptions.Center;
            _name.enableAutoSizing = true; _name.fontSizeMin = 18; _name.fontSizeMax = 30;

            _sub = MakeLabel(stage.transform, "Sub", "", fontUi, 12, UIColors.Soft, FontStyles.Normal);
            Anchor(_sub.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 44), new Vector2(-16, 62));
            _sub.alignment = TextAlignmentOptions.Center;

            var bar = MakeImage(stage.transform, "Bar", sprPill, UIColors.Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Anchor(bar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 32), new Vector2(-40, 38));
            var fill = MakeImage(bar.transform, "Fill", sprPill, UIColors.Green, Image.Type.Sliced);
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero; _fill.anchorMax = new Vector2(0, 1);
            _fill.offsetMin = Vector2.zero; _fill.offsetMax = Vector2.zero;

            _remaining = MakeLabel(stage.transform, "Remaining", "", fontUi, 13, UIColors.Green, FontStyles.Bold);
            Anchor(_remaining.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 8), new Vector2(-16, 28));
            _remaining.alignment = TextAlignmentOptions.Center;

            _pager = MakeLabel(stage.transform, "Pager", "", fontUi, 11, UIColors.Soft, FontStyles.Bold);
            Anchor(_pager.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -30), new Vector2(-12, -8));
            _pager.alignment = TextAlignmentOptions.Right;

            _prev = MakeArrow(stage.transform, "Prev", -1);
            _next = MakeArrow(stage.transform, "Next", 1);

            // ── Дві дії: Полив і Світло ──
            _water = MakeActionCard("Water", "Полив", sprWater, new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(16, 70), new Vector2(-5, 210), OnWater);
            _sun = MakeActionCard("Sun", "Світло", sprSun, new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(5, 70), new Vector2(-16, 210), OnSun);

            // ── Низ: ціна + підказка або «Зібрати» ──
            _price = MakeLabel(content, "Price", "", fontUi, 13, UIColors.GoldLt, FontStyles.Bold);
            Anchor(_price.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 46), new Vector2(-16, 64));
            _price.alignment = TextAlignmentOptions.Center;
            _price.enableAutoSizing = true; _price.fontSizeMin = 10; _price.fontSizeMax = 13;

            _hint = MakeLabel(content, "Hint", "Догляд не зупиняє ріст — лише додає до ціни. Свайп — сусідня рослина.",
                fontUi, 11, UIColors.Soft, FontStyles.Normal);
            Anchor(_hint.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(24, 4), new Vector2(-24, 40));
            _hint.alignment = TextAlignmentOptions.Center;
            _hint.textWrappingMode = TextWrappingModes.Normal;

            var harvestGo = MakeImage(content, "Harvest", sprPill, UIColors.Green, Image.Type.Sliced);
            harvestGo.raycastTarget = true;
            Anchor(harvestGo.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 0), new Vector2(-16, 42));
            _harvest = harvestGo.gameObject.AddComponent<Button>();
            _harvest.targetGraphic = harvestGo;
            _harvest.onClick.AddListener(OnHarvest);
            var hl = MakeLabel(harvestGo.transform, "Label", "Зібрати урожай", fontUi, 15, UIColors.Hex("#10160B"), FontStyles.Bold);
            Stretch(hl.rectTransform);
            hl.alignment = TextAlignmentOptions.Center;
        }

        private ActionCard MakeActionCard(string name, string title, Sprite icon, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, UnityEngine.Events.UnityAction onClick)
        {
            var card = new ActionCard();
            var bg = MakeImage(content, name, sprCard, UIColors.Rgba(Color.white, 0.05f), Image.Type.Sliced);
            bg.raycastTarget = true;
            Anchor(bg.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
            card.button = bg.gameObject.AddComponent<Button>();
            card.button.targetGraphic = bg;
            card.button.onClick.AddListener(onClick);
            // Неактивна картка не сіріє — стан показують рамка й підпис.
            var colors = card.button.colors;
            colors.disabledColor = Color.white;
            card.button.colors = colors;

            card.border = MakeImage(bg.transform, "Border", sprCardLine, Color.white, Image.Type.Sliced);
            Stretch(card.border.rectTransform);

            card.iconBg = MakeImage(bg.transform, "IconBg", sprCircle, Color.white, Image.Type.Simple);
            Place(card.iconBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(54, 54));
            card.icon = MakeImage(card.iconBg.transform, "Icon", icon, Color.white, Image.Type.Simple);
            card.icon.preserveAspect = true;
            Place(card.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));

            card.title = MakeLabel(bg.transform, "Title", title, fontHead, 22, UIColors.Text, FontStyles.Normal);
            Anchor(card.title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -94), new Vector2(-8, -68));
            card.title.alignment = TextAlignmentOptions.Center;

            card.status = MakeLabel(bg.transform, "Status", "", fontUi, 12, UIColors.Soft, FontStyles.Bold);
            Anchor(card.status.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 26), new Vector2(-8, 46));
            card.status.alignment = TextAlignmentOptions.Center;
            card.status.enableAutoSizing = true; card.status.fontSizeMin = 9; card.status.fontSizeMax = 12;

            card.counter = MakeLabel(bg.transform, "Counter", "", fontUi, 10.5f, UIColors.Soft, FontStyles.Normal);
            Anchor(card.counter.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 8), new Vector2(-8, 24));
            card.counter.alignment = TextAlignmentOptions.Center;
            card.counter.enableAutoSizing = true; card.counter.fontSizeMin = 8; card.counter.fontSizeMax = 10.5f;
            return card;
        }

        /// <summary>Кругла кнопка зі стрілкою (дві повернуті риски — без залежності від гліфа шрифту).</summary>
        private GameObject MakeArrow(Transform parent, string name, int dir)
        {
            var bg = MakeImage(parent, name, sprCircle, UIColors.Rgba(Color.white, 0.08f), Image.Type.Simple);
            bg.raycastTarget = true;
            float x = dir < 0 ? 0f : 1f;
            Place(bg.rectTransform, new Vector2(x, 0.62f), new Vector2(x, 0.5f), new Vector2(dir < 0 ? 10 : -10, 0), new Vector2(38, 38));
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => Step(dir));

            foreach (float angle in new[] { 45f, -45f })
            {
                var stroke = MakeImage(bg.transform, "Stroke", sprPill, UIColors.Text, Image.Type.Sliced);
                // «>» для dir=1: верхня риска нахилена вниз-праворуч, нижня — вгору-праворуч.
                float y = angle > 0 ? -3.5f : 3.5f;
                Place(stroke.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(12, 2.4f));
                stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, dir > 0 ? angle : -angle);
            }
            return bg.gameObject;
        }

        // ── Примітиви ─────────────────────────────────────────────────────
        private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sprite != null ? type : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
