using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Economy;
using PlantBreeding.Garden;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Сповіщення вгорі екрана — макет Сповіщення/export-notify (варіант 1b «Стопка зверху»):
    ///  • виконане завдання — головна картка: іконка-сувій, «Завдання виконано», «Зібрати 3 врожаї · 3/3»,
    ///    кнопка «Забрати» (одразу видає нагороду) і смужка прогресу;
    ///  • продаж — менший рядок під нею: іконка рослини, «Фіалка продана», «+12» і монета;
    ///  • решта повідомлень (GameEvents.OnToast) — такий самий рядок без іконки й суми.
    /// Під шапкою (нижче вирізу камери), з'являються згори з легким «пружинням»,
    /// важливіше (завдання) завжди вище й більше.
    /// </summary>
    public partial class EconomyOverlayView
    {
        [Header("Сповіщення (макет Сповіщення/export-notify)")]
        public Sprite sprCard;       // rounded-24
        public Sprite sprCardLine;
        public Sprite sprSmall;      // rounded-12 — іконки
        public Sprite sprSmallLine;
        public Sprite sprGlow;
        public Sprite sprCoin;
        public Sprite sprScroll;     // notify-scroll.svg

        private const float NoticeTop = 96f;      // під рядком шапки (HeaderRow: -44 … -86)
        private const float RowSeconds = 3.2f;
        private const float TaskSeconds = 7f;
        private const float EnterSeconds = 0.45f;
        private const float ExitSeconds = 0.3f;
        private const int MaxRows = 3;

        private enum NoticeKind { Row, Task }

        private class Notice
        {
            public NoticeKind kind;
            public string plantId, title;
            public int coins = -1;
            public int taskIndex = -1;

            public GameObject root;
            public RectTransform body;
            public CanvasGroup group;
            public float born, until;
        }

        private RectTransform _noticeStack;
        private readonly List<Notice> _shown = new List<Notice>();
        private readonly List<Notice> _waiting = new List<Notice>();
        private Sprite _barGradient;

        private static readonly Color CardBg = UIColors.Hex("#161E12");
        private static readonly Color RowBg = UIColors.Hex("#171F13");
        private static readonly Color Amount = UIColors.Hex("#F2D990");
        private static readonly Color RowText = UIColors.Hex("#C9CFC1");
        private static readonly Color SubText = UIColors.Hex("#A9B2A0");
        private static readonly Color Ink = UIColors.Hex("#0E130C");

        private void BuildNotifications()
        {
            var go = new GameObject("Notifications", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(_overlayRoot, false);
            _noticeStack = (RectTransform)go.transform;
            _noticeStack.anchorMin = new Vector2(0, 1); _noticeStack.anchorMax = new Vector2(1, 1);
            _noticeStack.pivot = new Vector2(0.5f, 1);
            _noticeStack.sizeDelta = new Vector2(-32, 0); // по 16 з боків — як шапка, сітка грядок і меню
            var vl = go.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 6; vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            PositionNotifications();
        }

        // ── Вхідні події ──────────────────────────────────────────────────
        private void EnqueueToast(string message)
        {
            if (!string.IsNullOrEmpty(message)) _waiting.Add(new Notice { kind = NoticeKind.Row, title = message });
        }

        private void EnqueueSale(string plantId, string title, int coins) =>
            _waiting.Add(new Notice { kind = NoticeKind.Row, plantId = plantId, title = title, coins = coins });

        private void EnqueueTask(int index) =>
            _waiting.Add(new Notice { kind = NoticeKind.Task, taskIndex = index });

        // ── Кадр ──────────────────────────────────────────────────────────
        private void UpdateNotifications()
        {
            if (_noticeStack == null) return;
            PositionNotifications();
            float now = Time.unscaledTime;

            // Нові: одна картка завдання за раз (завжди зверху), рядків — до MaxRows.
            for (int i = 0; i < _waiting.Count; i++)
            {
                var n = _waiting[i];
                bool taskBusy = n.kind == NoticeKind.Task && _shown.Exists(s => s.kind == NoticeKind.Task);
                bool rowsFull = n.kind == NoticeKind.Row && _shown.FindAll(s => s.kind == NoticeKind.Row).Count >= MaxRows;
                if (taskBusy || rowsFull) continue;
                _waiting.RemoveAt(i--);
                Show(n, now);
            }

            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                var n = _shown[i];
                float age = now - n.born;
                float enter = Mathf.Clamp01(age / EnterSeconds);
                float exit = Mathf.Clamp01((now - (n.until - ExitSeconds)) / ExitSeconds);
                if (now >= n.until)
                {
                    Destroy(n.root);
                    _shown.RemoveAt(i);
                    continue;
                }
                // Згори вниз з легким перельотом (cubic-bezier(.2,1.3,.4,1) у макеті), масштаб .94 → 1.
                float p = EaseOutBack(enter);
                n.body.anchoredPosition = new Vector2(0, 24f * (1f - p) + 12f * exit);
                n.body.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, p);
                n.group.alpha = Mathf.Clamp01(age / 0.3f) * (1f - exit);
            }
        }

        /// <summary>Під шапкою й нижче вирізу камери — оверлей SafeAreaFitter не зсуває.</summary>
        private void PositionNotifications()
        {
            float inset = SafeAreaFitter.TopInsetFraction * _overlayRoot.rect.height;
            _noticeStack.anchoredPosition = new Vector2(0, -(NoticeTop + inset));
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.7f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private void Show(Notice n, float now)
        {
            if (n.kind == NoticeKind.Task) BuildTaskCard(n);
            else BuildRow(n);
            n.born = now;
            n.until = now + (n.kind == NoticeKind.Task ? TaskSeconds : RowSeconds);
            if (n.kind == NoticeKind.Task) n.root.transform.SetAsFirstSibling(); // важливіше — вище
            _shown.Add(n);
        }

        private void Dismiss(Notice n)
        {
            float now = Time.unscaledTime;
            if (n.until - now > ExitSeconds) n.until = now + ExitSeconds;
        }

        // ── Картка «Завдання виконано» ───────────────────────────────────
        private void BuildTaskCard(Notice n)
        {
            var data = Core.GameManager.Instance != null ? Core.GameManager.Instance.playerData : null;
            var task = data != null && n.taskIndex >= 0 && n.taskIndex < data.dailyTasks.Count ? data.dailyTasks[n.taskIndex] : null;

            var body = MakeNoticeShell(n, 86);
            var bg = body.gameObject.AddComponent<Image>();
            bg.sprite = sprCard; bg.type = Image.Type.Sliced; bg.color = CardBg;
            bg.raycastTarget = true; // тап по картці не «протикає» у сад
            body.gameObject.AddComponent<RectMask2D>(); // світіння не виходить за картку

            var glow = MakeImage(body, "Glow", sprGlow, UIColors.Rgba(UIColors.GoldLt, 0.25f), Image.Type.Simple);
            Place(glow.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(60, -10), new Vector2(180, 110));
            var line = MakeImage(body, "Border", sprCardLine, UIColors.Rgba(UIColors.GoldLt, 0.40f), Image.Type.Sliced);
            Stretch(line.rectTransform);

            var iconBox = MakeImage(body, "IconBox", sprSmall, UIColors.Hex("#2F3D23"), Image.Type.Sliced);
            Place(iconBox.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(44, 44));
            var iconLine = MakeImage(iconBox.transform, "Border", sprSmallLine, UIColors.Rgba(UIColors.GoldLt, 0.35f), Image.Type.Sliced);
            Stretch(iconLine.rectTransform);
            var scroll = MakeImage(iconBox.transform, "Scroll", sprScroll, Color.white, Image.Type.Simple);
            scroll.preserveAspect = true;
            Place(scroll.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));

            var title = MakeLabel(body, "Title", "Завдання виконано", fontHead, 18, UIColors.Hex("#F2ECDF"), FontStyles.Normal);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(68, -34), new Vector2(-110, -12));
            title.enableAutoSizing = true; title.fontSizeMin = 13; title.fontSizeMax = 18;

            string desc = task != null ? $"{EconomyService.DescribeTask(task)} · {task.progress}/{task.target}" : "";
            var sub = MakeLabel(body, "Sub", desc, fontUi, 13, SubText, FontStyles.Normal);
            Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(68, -54), new Vector2(-110, -36));
            sub.enableAutoSizing = true; sub.fontSizeMin = 9; sub.fontSizeMax = 13;

            var claim = MakeClaimButton(body, "Забрати", () =>
            {
                int coins = task != null ? task.rewardCoins : 0;
                if (EconomyService.ClaimTask(n.taskIndex) && coins > 0) EnqueueSale(null, "Нагорода за завдання", coins);
                Dismiss(n);
            });
            Place((RectTransform)claim.button.transform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-12, -34), new Vector2(92, 36));
            claim.bg.color = UIColors.Green;
            claim.label.color = Ink;
            claim.label.fontSize = 13;

            var track = MakeImage(body, "Track", sprPill, UIColors.Rgba(Color.white, 0.08f), Image.Type.Sliced);
            Anchor(track.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(12, 14), new Vector2(-12, 19));
            float progress = task != null && task.target > 0 ? Mathf.Clamp01((float)task.progress / task.target) : 1f;
            var fill = MakeImage(track.transform, "Fill", BarGradient(), Color.white, Image.Type.Simple);
            Anchor(fill.rectTransform, Vector2.zero, new Vector2(progress, 1), Vector2.zero, Vector2.zero);
        }

        // ── Рядок продажу / повідомлення ─────────────────────────────────
        private void BuildRow(Notice n)
        {
            var body = MakeNoticeShell(n, 46);
            // Та сама ширина, що й решта елементів екрана; другорядність — менша висота й тьмяніший фон.
            var bg = body.gameObject.AddComponent<Image>();
            bg.sprite = sprRounded; bg.type = Image.Type.Sliced; bg.color = RowBg;
            bg.raycastTarget = false;
            var line = MakeImage(body, "Border", sprRoundedLine, UIColors.Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(line.rectTransform);

            float textLeft = 14f;
            var plant = string.IsNullOrEmpty(n.plantId) ? null : PlantCatalog.Get(n.plantId);
            if (plant != null)
            {
                var tint = plant.tint;
                var box = MakeImage(body, "IconBox", sprSmall, new Color(tint.r * 0.62f, tint.g * 0.45f, tint.b * 0.81f, 0.18f), Image.Type.Sliced);
                Place(box.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(30, 30));
                // Як у макеті — сама рослина без горщика, крупно. На полотні 200×260 рослина займає
                // ~130 px по висоті з центром на 58% зверху: масштаб під 28 px і зсув угору на 4.
                box.gameObject.AddComponent<RectMask2D>();
                var only = plant.GetPlantOnlySprite(1f);
                var art = only != null ? only : plant.sprite100 != null ? plant.sprite100 : plant.GetGrowthSprite(1f);
                var icon = MakeImage(box.transform, "Plant", art, Color.white, Image.Type.Simple);
                icon.preserveAspect = true;
                if (only != null) Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(44, 57));
                else Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(28, 28));
                textLeft = 48f;
            }

            bool hasAmount = n.coins >= 0;
            var text = MakeLabel(body, "Text", n.title, fontUi, 13, RowText, FontStyles.Bold);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(textLeft, 0), new Vector2(hasAmount ? -76 : -14, 0));
            text.enableAutoSizing = true; text.fontSizeMin = 9; text.fontSizeMax = 13;
            if (!hasAmount) text.alignment = TextAlignmentOptions.Center;

            if (hasAmount)
            {
                var coin = MakeImage(body, "Coin", sprCoin, Color.white, Image.Type.Simple);
                coin.preserveAspect = true;
                Place(coin.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(17, 17));
                var amount = MakeLabel(body, "Amount", $"+{n.coins}", fontUi, 14, Amount, FontStyles.Bold);
                Place(amount.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-35, 0), new Vector2(60, 24));
                amount.alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        /// <summary>Зовнішній елемент стопки (висота для лейауту, прозорість) + тіло, яке анімується.</summary>
        private RectTransform MakeNoticeShell(Notice n, float height)
        {
            var root = new GameObject(n.kind == NoticeKind.Task ? "TaskNotice" : "RowNotice",
                typeof(RectTransform), typeof(LayoutElement), typeof(CanvasGroup));
            root.transform.SetParent(_noticeStack, false);
            var le = root.GetComponent<LayoutElement>();
            le.preferredHeight = height; le.minHeight = height;
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = n.kind == NoticeKind.Task; // рядки не заважають тапати по саду

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(root.transform, false);
            var brt = (RectTransform)body.transform;
            Stretch(brt);
            brt.pivot = new Vector2(0.5f, 1f);

            n.root = root; n.body = brt; n.group = group;
            return brt;
        }

        /// <summary>Смужка прогресу зелений → золотий (як linear-gradient у макеті).</summary>
        private Sprite BarGradient()
        {
            if (_barGradient != null) return _barGradient;
            var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var a = UIColors.Green; var b = UIColors.GoldLt;
            for (int x = 0; x < 64; x++) tex.SetPixel(x, 0, Color.Lerp(a, b, x / 63f));
            tex.Apply();
            _barGradient = Sprite.Create(tex, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
            return _barGradient;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }
    }
}
