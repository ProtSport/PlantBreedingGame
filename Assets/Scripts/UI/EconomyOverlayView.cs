using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Economy;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Шар економіки поверх екрану «Мій сад» (docs/ECONOMY.md):
    ///  • тости нагород ("+15 монет", "Рівень 2!") з GameEvents.OnToast;
    ///  • панель «Завдання дня» (кнопка-зірка в шапці HeaderView будь-якого
    ///    екрану → OpenTasks): нагорода за вхід зі стріком 7 днів, 3 щоденні
    ///    завдання, скриня за всі три;
    ///  • вікно прискорення росту за кристали (EconomyOverlayView.SpeedUp.cs);
    ///  • Крамниця кристалів (EconomyOverlayView.Shop.cs → OpenShop) — пілюля
    ///    кристалів у шапці будь-якого екрану і пункт «Крамниця» нижнього меню;
    ///  • кнопка «Зібрати все» (EconomyOverlayView.HarvestAll.cs);
    ///  • вікно лікування хвороб/шкідників (EconomyOverlayView.Treatment.cs);
    ///  • нижній лист «Налаштування» — тема й мова (EconomyOverlayView.Settings.cs).
    ///
    /// Сцена саду завантажена завжди (інші екрани — additive поверх неї), тож
    /// Instance доступний з будь-якого екрану, а вікна (sortingOrder 30) — над ними.
    ///
    /// Посилання (шрифти/спрайти/пункти меню) заповнює
    /// Assets/Editor/HomeScreenBuilder.cs, а сам UI будується тут у Start() ОДИН
    /// раз і більше ніколи не знищується — тапи лише міняють тексти/стан
    /// (той самий підхід, що PlantingScreenController, через InputSystem hover-баг).
    /// </summary>
    public partial class EconomyOverlayView : MonoBehaviour
    {
        public static EconomyOverlayView Instance { get; private set; }

        [Header("Посилання зі сцени")]
        [Tooltip("Пункт «Крамниця» нижнього меню (декоративний у білдері — клік додається тут)")]
        public RectTransform shopNavItem;

        [Header("Спільна графіка (з UIBuilderKit)")]
        public Sprite sprRounded;
        public Sprite sprRoundedLine;
        public Sprite sprPill;
        public Sprite sprPillLine;
        public Sprite sprCircle;
        public TMP_FontAsset fontHead;
        public TMP_FontAsset fontUi;

        private const int ToastSlots = 3;
        private const float ToastSeconds = 3.2f;

        private class ClaimButton
        {
            public Button button;
            public Image bg;
            public TMP_Text label;
        }

        private class TaskRow
        {
            public GameObject root;
            public TMP_Text desc, reward, progressText;
            public RectTransform fill;
            public ClaimButton claim;
        }

        private RectTransform _overlayRoot;
        private GameObject _modal;
        private TMP_Text _streakLabel, _loginTitle, _loginReward;
        private readonly List<Image> _streakCells = new List<Image>();
        private readonly List<TMP_Text> _streakCellLabels = new List<TMP_Text>();
        private ClaimButton _loginClaim;
        private readonly List<TaskRow> _taskRows = new List<TaskRow>();
        private TMP_Text _chestLabel;
        private ClaimButton _chestClaim;

        private readonly List<CanvasGroup> _toasts = new List<CanvasGroup>();
        private readonly List<TMP_Text> _toastLabels = new List<TMP_Text>();
        private readonly float[] _toastUntil = new float[ToastSlots];
        private readonly Queue<string> _toastQueue = new Queue<string>();

        private void OnEnable()
        {
            Instance = this;
            GameEvents.OnToast += EnqueueToast;
            GameEvents.OnDailyStateChanged += RefreshAll;
            GameEvents.OnCurrencyChanged += RefreshAll;
            GameEvents.OnDexBadgeChanged += RefreshDexBadge;
            GardenManager.GrowingPlotTapped += OpenSpeedUp;
            GardenManager.SickPlotTapped += OpenTreatment;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameEvents.OnToast -= EnqueueToast;
            GameEvents.OnDailyStateChanged -= RefreshAll;
            GameEvents.OnCurrencyChanged -= RefreshAll;
            GameEvents.OnDexBadgeChanged -= RefreshDexBadge;
            GardenManager.GrowingPlotTapped -= OpenSpeedUp;
            GardenManager.SickPlotTapped -= OpenTreatment;
        }

        private void Start()
        {
            BuildOverlayRoot();
            BuildHarvestAllButton();
            BuildToasts();
            BuildModal();
            BuildSpeedUpModal();
            BuildTreatmentModal();
            BuildShopModal();
            BuildSettingsSheet();
            HookShopEntryPoints();
            BuildDexBadge();

            _modal.SetActive(false);
            _speedModal.SetActive(false);
            _treatModal.SetActive(false);
            _shopModal.SetActive(false);
            RefreshAll();
            RefreshHarvestAll();
            RefreshDexBadge();
            StartCoroutine(ShowSessionHints());
        }

        /// <summary>Перший кадр після запуску: нагорода за вхід або підказка новачку.</summary>
        private IEnumerator ShowSessionHints()
        {
            yield return null;
            // Грядки, відновлені зі сейва вже дозрілими, не шлють подій — перерахувати
            // після GardenManager.Start (порядок Start між об'єктами не гарантовано).
            RefreshHarvestAll();
            var gm = GameManager.Instance;
            if (gm == null) yield break;
            var data = gm.playerData;

            if (EconomyService.IsLoginRewardAvailable(data))
            {
                OpenTasks();
            }
            else if (data.totalHarvests == 0)
            {
                EnqueueToast("Тапни порожню грядку й посади фіалку — вона виросте за 3 хвилини");
            }
        }

        private void Update()
        {
            UpdateSpeedUp();

            for (int i = 0; i < _toasts.Count; i++)
            {
                var cg = _toasts[i];
                if (cg.alpha <= 0f) continue;
                float left = _toastUntil[i] - Time.unscaledTime;
                cg.alpha = Mathf.Clamp01(left / 0.4f);
            }

            if (_toastQueue.Count > 0)
            {
                for (int i = 0; i < _toasts.Count && _toastQueue.Count > 0; i++)
                {
                    if (_toasts[i].alpha > 0f) continue;
                    _toastLabels[i].text = _toastQueue.Dequeue();
                    _toasts[i].alpha = 1f;
                    _toastUntil[i] = Time.unscaledTime + ToastSeconds;
                    _toasts[i].transform.SetAsLastSibling();
                }
            }
        }

        private void EnqueueToast(string message)
        {
            if (!string.IsNullOrEmpty(message)) _toastQueue.Enqueue(message);
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОБУДОВА (один раз)
        // ════════════════════════════════════════════════════════════════
        private void BuildOverlayRoot()
        {
            // Власний Canvas із сортуванням поверх саду (і поверх Посадки = 10),
            // щоб тости про урожай/рівень було видно на будь-якому екрані.
            var go = new GameObject("EconomyOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;
            _overlayRoot = (RectTransform)go.transform;
            Stretch(_overlayRoot);
        }

        private void BuildToasts()
        {
            var stack = new GameObject("Toasts", typeof(RectTransform), typeof(VerticalLayoutGroup));
            stack.transform.SetParent(_overlayRoot, false);
            var rt = (RectTransform)stack.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -96);
            rt.sizeDelta = new Vector2(-32, 3 * 50);
            var vl = stack.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 6; vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = true; vl.childControlHeight = false;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

            for (int i = 0; i < ToastSlots; i++)
            {
                var toast = new GameObject("Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                toast.transform.SetParent(stack.transform, false);
                ((RectTransform)toast.transform).sizeDelta = new Vector2(0, 44);
                var bg = toast.GetComponent<Image>();
                bg.sprite = sprPill; bg.type = Image.Type.Sliced;
                bg.color = UIColors.Rgba(UIColors.Hex("#161E10"), 0.96f);
                bg.raycastTarget = false;
                var line = MakeImage(toast.transform, "Border", sprPillLine, UIColors.Rgba(UIColors.GoldLt, 0.45f), Image.Type.Sliced);
                Stretch(line.rectTransform);

                var label = MakeLabel(toast.transform, "Text", "", fontUi, 13, UIColors.Text, FontStyles.Bold);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(16, 2);
                label.rectTransform.offsetMax = new Vector2(-16, -2);
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 9; label.fontSizeMax = 13;

                var cg = toast.GetComponent<CanvasGroup>();
                cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
                _toasts.Add(cg);
                _toastLabels.Add(label);
            }
        }

        /// <summary>
        /// Затемнення на весь екран (ловить тапи — сад під ним неактивний) +
        /// центрована панель з вертикальним лейаутом, висота — за вмістом,
        /// і хрестик у правому верхньому куті (onClose — закриття цього вікна).
        /// </summary>
        private GameObject MakeModal(string name, System.Action onClose, out Transform panelContent)
        {
            var modal = new GameObject(name, typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(_overlayRoot, false);
            Stretch((RectTransform)modal.transform);
            modal.GetComponent<Image>().color = new Color(0, 0, 0, 0.62f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(modal.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(358, 0);
            var pbg = panel.GetComponent<Image>();
            pbg.sprite = sprRounded; pbg.type = Image.Type.Sliced;
            pbg.color = UIColors.Hex("#141B0F");
            var pline = MakeImage(panel.transform, "Border", sprRoundedLine, UIColors.Rgba(Color.white, 0.10f), Image.Type.Sliced);
            pline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(pline.rectTransform);

            var vl = panel.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(16, 16, 18, 16);
            vl.spacing = 10;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MakeCloseCross(panel.transform, onClose);

            panelContent = panel.transform;
            return modal;
        }

        /// <summary>Кругла кнопка «×» (дві навхрест повернуті риски — без залежності від гліфа шрифту).</summary>
        private void MakeCloseCross(Transform panel, System.Action onClose)
        {
            var go = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(panel, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            Place((RectTransform)go.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10), new Vector2(34, 34));
            var bg = go.GetComponent<Image>();
            bg.sprite = sprCircle;
            bg.color = UIColors.Rgba(Color.white, 0.08f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClose());

            foreach (float angle in new[] { 45f, -45f })
            {
                var bar = MakeImage(go.transform, "Bar", sprPill, UIColors.Text, Image.Type.Sliced);
                Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 2.4f));
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }

        private void BuildModal()
        {
            _modal = MakeModal("TasksModal", CloseModal, out var panel);

            var title = MakeLabel(panel, "Title", "Завдання дня", fontHead, 28, UIColors.Text, FontStyles.Normal);
            SetHeight(title.gameObject, 32);
            _streakLabel = MakeLabel(panel, "Streak", "", fontUi, 11, UIColors.Soft, FontStyles.Normal);
            SetHeight(_streakLabel.gameObject, 14);

            BuildLoginRow(panel);
            for (int i = 0; i < EconomyConfig.DailyTaskCount; i++) BuildTaskRow(panel);
            BuildChestRow(panel);

            var close = MakeClaimButton(panel, "Закрити", CloseModal);
            SetHeight(close.button.gameObject, 40);
            close.bg.color = UIColors.Rgba(Color.white, 0.08f);
        }

        private void BuildLoginRow(Transform parent)
        {
            var row = MakeRow(parent, "LoginRow", 112, UIColors.Rgba(UIColors.GoldLt, 0.10f));

            _loginTitle = MakeLabel(row, "Title", "", fontUi, 13, UIColors.GoldLt, FontStyles.Bold);
            Place(_loginTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(210, 18));

            _loginReward = MakeLabel(row, "Reward", "", fontUi, 11, UIColors.Text, FontStyles.Normal);
            Place(_loginReward.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -30), new Vector2(210, 30));
            _loginReward.textWrappingMode = TextWrappingModes.Normal;

            // 7 кружечків стріку: пройдені — зелені, сьогодні — золотий, попереду — тьмяні.
            for (int i = 0; i < EconomyConfig.LoginCycle.Length; i++)
            {
                var cell = MakeImage(row, "Day" + (i + 1), sprCircle, Color.white, Image.Type.Simple);
                Place(cell.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12 + i * 30, 10), new Vector2(24, 24));
                var num = MakeLabel(cell.transform, "N", (i + 1).ToString(), fontUi, 11, UIColors.Text, FontStyles.Bold);
                Stretch(num.rectTransform);
                num.alignment = TextAlignmentOptions.Center;
                _streakCells.Add(cell);
                _streakCellLabels.Add(num);
            }

            _loginClaim = MakeClaimButton(row, "Забрати", () => EconomyService.ClaimLoginReward());
            Place((RectTransform)_loginClaim.button.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 8), new Vector2(92, 36));
        }

        private void BuildTaskRow(Transform parent)
        {
            int index = _taskRows.Count;
            var row = MakeRow(parent, "Task" + index, 68, UIColors.Rgba(Color.white, 0.05f));
            var h = new TaskRow { root = row.gameObject };

            h.desc = MakeLabel(row, "Desc", "", fontUi, 13, UIColors.Text, FontStyles.Bold);
            Place(h.desc.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -9), new Vector2(214, 18));
            h.desc.enableAutoSizing = true; h.desc.fontSizeMin = 10; h.desc.fontSizeMax = 13;

            h.reward = MakeLabel(row, "Reward", "", fontUi, 11, UIColors.GoldLt, FontStyles.Normal);
            Place(h.reward.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -29), new Vector2(214, 14));

            var barBg = MakeImage(row, "Bar", sprPill, UIColors.Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Place(barBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 12), new Vector2(168, 6));
            var fill = MakeImage(barBg.transform, "Fill", sprPill, UIColors.Green, Image.Type.Sliced);
            h.fill = fill.rectTransform;
            h.fill.anchorMin = Vector2.zero; h.fill.anchorMax = new Vector2(0, 1);
            h.fill.offsetMin = Vector2.zero; h.fill.offsetMax = Vector2.zero;

            h.progressText = MakeLabel(row, "Progress", "", fontUi, 10, UIColors.Soft, FontStyles.Normal);
            Place(h.progressText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(186, 6), new Vector2(48, 16));

            h.claim = MakeClaimButton(row, "Забрати", () => EconomyService.ClaimTask(index));
            Place((RectTransform)h.claim.button.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(92, 36));

            _taskRows.Add(h);
        }

        private void BuildChestRow(Transform parent)
        {
            var row = MakeRow(parent, "ChestRow", 56, UIColors.Rgba(UIColors.Blue, 0.08f));
            _chestLabel = MakeLabel(row, "Label", "", fontUi, 12, UIColors.Text, FontStyles.Bold);
            Place(_chestLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(214, 40));
            _chestLabel.textWrappingMode = TextWrappingModes.Normal;

            _chestClaim = MakeClaimButton(row, "Відкрити", () => EconomyService.ClaimChest());
            Place((RectTransform)_chestClaim.button.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(92, 36));
        }

        // ════════════════════════════════════════════════════════════════
        //  ВІДМАЛЬОВКА
        // ════════════════════════════════════════════════════════════════
        /// <summary>Панель «Завдання дня» — з кнопки-зірки шапки будь-якого екрану.</summary>
        public void OpenTasks()
        {
            var gm = GameManager.Instance;
            if (gm != null) EconomyService.RollDay(gm.playerData); // гра могла висіти відкритою через північ
            _modal.SetActive(true);
            RefreshAll();
        }

        private void CloseModal() => _modal.SetActive(false);

        private void RefreshAll()
        {
            var gm = GameManager.Instance;
            if (gm == null || _modal == null) return;
            var data = gm.playerData;

            RefreshShop();

            if (!_modal.activeSelf) return;

            // ── Вхід ──
            bool loginAvailable = EconomyService.IsLoginRewardAvailable(data);
            int cycleIdx = EconomyService.LoginCycleIndex(data);
            _streakLabel.text = data.loginStreak > 0
                ? $"Стрік входів: {data.loginStreak} дн. · нові завдання щодня опівночі"
                : "Заходь завтра — почнеться стрік щоденних нагород";
            _loginTitle.text = $"Нагорода за вхід · день {cycleIdx + 1} з 7";

            if (data.loginStreak <= 0)
            {
                _loginReward.text = $"Завтра: {EconomyService.FormatReward(EconomyService.GetLoginReward(data, 0))}";
            }
            else if (loginAvailable)
            {
                _loginReward.text = EconomyService.FormatReward(EconomyService.GetLoginReward(data, cycleIdx));
            }
            else
            {
                int next = (cycleIdx + 1) % EconomyConfig.LoginCycle.Length;
                _loginReward.text = $"Отримано. Завтра: {EconomyService.FormatReward(EconomyService.GetLoginReward(data, next))}";
            }
            SetClaim(_loginClaim, loginAvailable, loginAvailable ? "Забрати" : "Завтра");

            for (int i = 0; i < _streakCells.Count; i++)
            {
                bool today = data.loginStreak > 0 && i == cycleIdx;
                bool past = data.loginStreak > 0 && (i < cycleIdx || (today && !loginAvailable));
                _streakCells[i].color = past ? UIColors.Rgba(UIColors.Green, 0.55f)
                    : today ? UIColors.Rgba(UIColors.GoldLt, 0.85f)
                    : UIColors.Rgba(Color.white, 0.08f);
                _streakCellLabels[i].color = past || today ? UIColors.Hex("#10160B") : UIColors.Soft;
            }

            // ── Завдання ──
            for (int i = 0; i < _taskRows.Count; i++)
            {
                var row = _taskRows[i];
                bool exists = i < data.dailyTasks.Count;
                row.root.SetActive(exists);
                if (!exists) continue;

                var t = data.dailyTasks[i];
                row.desc.text = EconomyService.DescribeTask(t);
                row.reward.text = $"+{t.rewardCoins} монет · +{t.rewardXp} XP";
                row.progressText.text = $"{t.progress}/{t.target}";
                row.fill.anchorMax = new Vector2(t.target > 0 ? Mathf.Clamp01((float)t.progress / t.target) : 0f, 1);
                SetClaim(row.claim, t.IsComplete && !t.claimed, t.claimed ? "Отримано" : t.IsComplete ? "Забрати" : "В процесі");
            }

            // ── Скриня ──
            bool chest = EconomyService.IsChestAvailable(data);
            _chestLabel.text = data.dailyChestClaimed
                ? "Скриню дня відкрито. Нові завдання — завтра!"
                : $"Скриня дня: виконай усі завдання → +{EconomyConfig.DailyChestCoins(data.level)} монет · +{EconomyConfig.DailyChestXp} XP";
            SetClaim(_chestClaim, chest, data.dailyChestClaimed ? "Відкрито" : "Відкрити");
        }

        private static void SetClaim(ClaimButton c, bool active, string text)
        {
            c.button.interactable = active;
            c.label.text = text;
            c.bg.color = active ? UIColors.Green : UIColors.Rgba(Color.white, 0.06f);
            c.label.color = active ? UIColors.Hex("#10160B") : UIColors.Soft;
        }

        // ════════════════════════════════════════════════════════════════
        //  ПРИМІТИВИ
        // ════════════════════════════════════════════════════════════════
        private Transform MakeRow(Transform parent, string name, float height, Color color)
        {
            var row = MakeImage(parent, name, sprRounded, color, Image.Type.Sliced);
            SetHeight(row.gameObject, height);
            return row.transform;
        }

        private ClaimButton MakeClaimButton(Transform parent, string text, System.Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var bg = go.GetComponent<Image>();
            bg.sprite = sprPill; bg.type = Image.Type.Sliced;
            bg.color = UIColors.Green;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick());

            var label = MakeLabel(go.transform, "Label", text, fontUi, 12, UIColors.Hex("#10160B"), FontStyles.Bold);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            return new ClaimButton { button = btn, bg = bg, label = label };
        }

        private static void SetHeight(GameObject go, float height)
        {
            // Без "??" — UnityEngine.Object має власне порівняння з null.
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

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
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
