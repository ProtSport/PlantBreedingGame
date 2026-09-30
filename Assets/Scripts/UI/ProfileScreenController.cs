using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Collections;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Логіка екрану «Профіль» (ТЗ «Персонаж», Персонаж/export/profile-screen.html).
    /// Assets/Editor/ProfileScreenBuilder.cs будує статичний каркас (шапка,
    /// скрол-контейнер, оверлей перейменування, тост), цей скрипт наповнює
    /// контент — рівно один раз у Start(), як DexScreenController/
    /// LabScreenController (картки більше НІКОЛИ не знищуються).
    /// </summary>
    public class ProfileScreenController : MonoBehaviour
    {
        [Header("Шапка")]
        public Button backButton;
        public Button settingsButton;

        [Header("Контент (наповнюється в рантаймі)")]
        public Transform contentRoot;

        [Header("Оверлей перейменування")]
        public GameObject renamePanel;
        public TMP_InputField renameInput;
        public Button renameConfirmButton;
        public Button renameCancelButton;

        [Header("Тост")]
        public GameObject toastRoot;
        public TMP_Text toastLabel;

        [Header("Спільна графіка (з UIBuilderKit)")]
        public Sprite sprRoundedCard;
        public Sprite sprRoundedCardLine;
        public Sprite sprRoundedSmall;
        public Sprite sprRoundedSmallLine;
        public Sprite sprPillTiny;
        public Sprite sprSprig;
        public Sprite sprIconCheck;
        public Sprite sprCircleFill;
        public Sprite sprCircleLine;
        public Sprite sprGlow;
        public TMP_FontAsset fontHead;
        public TMP_FontAsset fontUi;

        private TMP_Text _nameLabel;
        private GameObject _renameChip;
        private TMP_Text _rankLabel;
        private TMP_Text _levelLabel;
        private Image _xpFill;
        private TMP_Text _xpLabel;
        private Image _heroRing;
        private Transform _heroSparkles;
        private float _toastTimer;

        // ════════════════════════════════════════════════════════════════
        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(Close);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => ShowToast("Скоро"));
            if (renameConfirmButton != null) renameConfirmButton.onClick.AddListener(ConfirmRename);
            if (renameCancelButton != null) renameCancelButton.onClick.AddListener(() => renamePanel.SetActive(false));

            BuildContent();
            RefreshAll();

            if (renamePanel != null) renamePanel.SetActive(false);
            if (toastRoot != null) toastRoot.SetActive(false);
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f && toastRoot != null) toastRoot.SetActive(false);
            }
        }

        private void Close()
        {
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("Profile");
        }

        // ════════════════════════════════════════════════════════════════
        //  ПОБУДОВА (один раз)
        // ════════════════════════════════════════════════════════════════
        private void BuildContent()
        {
            BuildAvatarHero(contentRoot);
            BuildNameBlock(contentRoot);
            BuildRankRow(contentRoot);
            BuildXpBar(contentRoot);
            BuildSectionHead(contentRoot, "Іконки аватара", out var avatarCountLabel);
            BuildAvatarGrid(contentRoot);
            var lockedHint = MakeLabel(contentRoot, "AvatarHint", "", fontUi, 11.5f, ColMut, FontStyles.Normal);
            lockedHint.alignment = TextAlignmentOptions.MidlineLeft;
            lockedHint.text = "Заблоковані — нагороди Завдань тижня";
            BuildSectionHead(contentRoot, "Рамки профілю", out var frameCountLabel);
            BuildFrameGrid(contentRoot);

            _avatarCountLabel = avatarCountLabel;
            _frameCountLabel = frameCountLabel;
        }

        private Image _heroIcon;
        private TMP_Text _avatarCountLabel;
        private TMP_Text _frameCountLabel;
        private readonly List<(AvatarDef def, Image bg, Image icon, GameObject checkBadge, GameObject lockBadge)> _avatarCells = new();
        private readonly List<(FrameDef def, Image ring, GameObject checkBadge, GameObject lockBadge, TMP_Text label)> _frameCells = new();

        private void BuildAvatarHero(Transform parent)
        {
            var wrap = new GameObject("HeroWrap", typeof(RectTransform), typeof(LayoutElement));
            wrap.transform.SetParent(parent, false);
            wrap.GetComponent<LayoutElement>().preferredHeight = 168;

            var hero = new GameObject("AvatarHero", typeof(RectTransform));
            hero.transform.SetParent(wrap.transform, false);
            var hrt = (RectTransform)hero.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(168, 168);
            hrt.anchoredPosition = Vector2.zero;

            var ring = MakeImage(hero.transform, "Ring", sprCircleLine, ColGold, Image.Type.Simple);
            Stretch(ring.rectTransform);
            _heroRing = ring;

            var faceBg = MakeImage(hero.transform, "Face", sprCircleFill, Hex("#1C2414"), Image.Type.Simple);
            Place(faceBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156, 156));

            var sparkles = new GameObject("Sparkles", typeof(RectTransform));
            sparkles.transform.SetParent(hero.transform, false);
            Stretch((RectTransform)sparkles.transform);
            _heroSparkles = sparkles.transform;
            AddSparkle(sparkles.transform, new Vector2(58, 62), 16);
            AddSparkle(sparkles.transform, new Vector2(-64, -50), 12);
            AddSparkle(sparkles.transform, new Vector2(-78, 30), 9);

            var icon = MakeImage(hero.transform, "Sprig", sprSprig, Hex("#DDE8C8"), Image.Type.Simple);
            icon.preserveAspect = true;
            _heroIcon = icon;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(72, 72));
        }

        private void AddSparkle(Transform parent, Vector2 pos, float size)
        {
            var s = MakeImage(parent, "Spark", sprGlow, Hex("#F6E4A6"), Image.Type.Simple);
            Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size * 2.4f, size * 2.4f));
        }

        private void BuildNameBlock(Transform parent)
        {
            var row = MakeRow(parent, 40, 9);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _nameLabel = MakeLabel(row.transform, "Name", "", fontHead, 30, ColText, FontStyles.Normal);
            _nameLabel.alignment = TextAlignmentOptions.Center;

            var editBtn = MakeActionButton(row.transform, "Змінити", small: true);
            editBtn.onClick.AddListener(OpenRename);

            _renameChip = new GameObject("RenameChip", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            _renameChip.transform.SetParent(parent, false);
            var rhl = _renameChip.GetComponent<HorizontalLayoutGroup>();
            rhl.padding = new RectOffset(11, 11, 4, 4); rhl.childAlignment = TextAnchor.MiddleCenter;
            rhl.childControlWidth = true; rhl.childControlHeight = true;
            rhl.childForceExpandWidth = false; rhl.childForceExpandHeight = false;
            _renameChip.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _renameChip.GetComponent<LayoutElement>().preferredHeight = 24;
            var chipBg = _renameChip.AddComponent<Image>();
            chipBg.sprite = sprPillTiny; chipBg.type = Image.Type.Sliced; chipBg.color = Rgba(ColViolet, 0.12f);
            var chipBorder = MakeImage(_renameChip.transform, "Border", sprPillTiny, Rgba(ColViolet, 0.3f), Image.Type.Sliced);
            Stretch(chipBorder.rectTransform);
            chipBorder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            _renameChipLabel = MakeLabel(_renameChip.transform, "Label", "", fontUi, 11, ColViolet, FontStyles.Bold);
        }

        private TMP_Text _renameChipLabel;

        private void BuildRankRow(Transform parent)
        {
            var row = MakeRow(parent, 30, 8);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var rankPill = MakePill(row.transform, "", Hex("#3A2A0C"), ColGold, ColGoldLt);
            rankPill.fontSize = 13;
            _rankLabel = rankPill;
            _levelLabel = MakeLabel(row.transform, "Level", "", fontUi, 13, ColMut, FontStyles.Bold);
        }

        private void BuildXpBar(Transform parent)
        {
            var wrap = new GameObject("XpWrap", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            wrap.transform.SetParent(parent, false);
            wrap.GetComponent<LayoutElement>().preferredWidth = 220;
            wrap.GetComponent<LayoutElement>().preferredHeight = 24;
            var vl = wrap.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 5; vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

            var track = MakeImage(wrap.transform, "Track", sprPillTiny, Rgba(Color.white, 0.09f), Image.Type.Sliced);
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 7;
            var fill = MakeImage(track.transform, "Fill", sprPillTiny, ColGreen, Image.Type.Sliced);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0, 1);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            _xpFill = fill;

            _xpLabel = MakeLabel(wrap.transform, "Text", "", fontUi, 11, ColMut, FontStyles.Normal);
            _xpLabel.alignment = TextAlignmentOptions.Center;
        }

        private void BuildSectionHead(Transform parent, string title, out TMP_Text countLabel)
        {
            var row = MakeRow(parent, 30, 0);
            var t = MakeLabel(row.transform, "Title", title, fontHead, 22, ColText, FontStyles.Normal);
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            countLabel = MakeLabel(row.transform, "Count", "", fontUi, 12, ColMut, FontStyles.Normal);
        }

        private void BuildAvatarGrid(Transform parent)
        {
            var grid = new GameObject("AvatarGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(parent, false);
            var gl = grid.GetComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(80, 80); gl.spacing = new Vector2(11, 11);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 4;
            int avRows = (ProfileItemCatalog.Avatars.Length + 3) / 4;
            grid.GetComponent<LayoutElement>().preferredHeight = avRows * 80 + (avRows - 1) * 11;

            foreach (var def in ProfileItemCatalog.Avatars)
            {
                var cell = new GameObject("Av_" + def.id, typeof(RectTransform), typeof(Image), typeof(Button));
                cell.transform.SetParent(grid.transform, false);
                var bg = cell.GetComponent<Image>();
                bg.sprite = sprRoundedCard; bg.type = Image.Type.Sliced;
                var border = MakeImage(cell.transform, "Border", sprRoundedCardLine, Rgba(Color.white, 0.1f), Image.Type.Sliced);
                Stretch(border.rectTransform);
                var icon = MakeImage(cell.transform, "Sprig", sprSprig, Hex(def.tintHex), Image.Type.Simple);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36));
                var check = MakeCheckBadge(cell.transform);
                var lockBadge = MakeLockBadge(cell.transform);

                var btn = cell.GetComponent<Button>();
                btn.targetGraphic = bg;
                var id = def.id;
                btn.onClick.AddListener(() => OnAvatarTapped(id));

                _avatarCells.Add((def, bg, icon, check, lockBadge));
            }
        }

        private void BuildFrameGrid(Transform parent)
        {
            var grid = new GameObject("FrameGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(parent, false);
            var gl = grid.GetComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(80, 96); gl.spacing = new Vector2(11, 11);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 4;
            int frRows = (ProfileItemCatalog.Frames.Length + 3) / 4;
            grid.GetComponent<LayoutElement>().preferredHeight = frRows * 96 + (frRows - 1) * 11;

            foreach (var def in ProfileItemCatalog.Frames)
            {
                var col = new GameObject("Fr_" + def.id, typeof(RectTransform), typeof(VerticalLayoutGroup));
                col.transform.SetParent(grid.transform, false);
                var vl = col.GetComponent<VerticalLayoutGroup>();
                vl.spacing = 6; vl.childAlignment = TextAnchor.UpperCenter;
                vl.childControlWidth = true; vl.childControlHeight = true;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

                var ringWrap = new GameObject("RingWrap", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                ringWrap.transform.SetParent(col.transform, false);
                ringWrap.GetComponent<LayoutElement>().preferredHeight = 80;
                var ring = ringWrap.GetComponent<Image>();
                ring.sprite = sprCircleLine; ring.color = Hex(def.colorHex);
                ring.raycastTarget = true;
                var face = MakeImage(ringWrap.transform, "Face", sprCircleFill, Hex("#1C2414"), Image.Type.Simple);
                Place(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(68, 68));
                var check = MakeCheckBadge(ringWrap.transform);
                var lockBadge = MakeLockBadge(ringWrap.transform);

                var btn = ringWrap.GetComponent<Button>();
                btn.targetGraphic = ring;
                var id = def.id;
                btn.onClick.AddListener(() => OnFrameTapped(id));

                var label = MakeLabel(col.transform, "Label", def.label, fontUi, 10.5f, ColMut, FontStyles.Normal);
                label.alignment = TextAlignmentOptions.Center;

                _frameCells.Add((def, ring, check, lockBadge, label));
            }
        }

        private GameObject MakeCheckBadge(Transform parent)
        {
            var badge = MakeImage(parent, "CheckBadge", sprCircleFill, ColGreenDeep, Image.Type.Simple);
            Place(badge.rectTransform, new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-4, 4), new Vector2(20, 20));
            var check = MakeImage(badge.transform, "Check", sprIconCheck, Hex("#0E130C"), Image.Type.Simple);
            check.preserveAspect = true;
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11, 11));
            badge.gameObject.SetActive(false);
            return badge.gameObject;
        }

        // "?" замість силуету — той самий патерн, що DexScreenController.BuildDetailSlot
        // для нерозкритих слотів колекції.
        private GameObject MakeLockBadge(Transform parent)
        {
            var q = MakeLabel(parent, "LockQ", "?", fontHead, 28, Hex("#4E5847"), FontStyles.Normal);
            Stretch(q.rectTransform);
            q.alignment = TextAlignmentOptions.Center;
            q.gameObject.SetActive(false);
            return q.gameObject;
        }

        // ════════════════════════════════════════════════════════════════
        //  ВІДМАЛЬОВКА
        // ════════════════════════════════════════════════════════════════
        private void RefreshAll()
        {
            var gm = GameManager.Instance;
            var data = gm != null ? gm.playerData : null;
            if (data == null) return;

            string tier = gm.GetPrestigeTier();
            _heroRing.color = TierColor(tier);
            var activeAvatar = ProfileItemCatalog.FindAvatar(data.activeAvatarId);
            if (_heroIcon != null) _heroIcon.color = Hex(activeAvatar != null ? activeAvatar.tintHex : "#DDE8C8");
            if (_heroSparkles != null) _heroSparkles.gameObject.SetActive(tier == "spark");

            _nameLabel.text = data.playerName;
            bool hasToken = data.renameTokens > 0;
            _renameChip.SetActive(hasToken);
            if (hasToken) _renameChipLabel.text = data.renameTokens + (data.renameTokens == 1 ? " токен зміни імені" : " токени зміни імені");

            _rankLabel.text = gm.GetRankLabel();
            _levelLabel.text = "Рівень " + data.level;

            int xpForNext = 100 + (data.level - 1) * 50;
            _xpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)data.xp / xpForNext), 1);
            _xpLabel.text = $"{data.xp} / {xpForNext} XP до {data.level + 1} рівня";

            int avOwned = 0;
            foreach (var cell in _avatarCells)
            {
                bool owned = gm.IsAvatarOwned(cell.def.id);
                bool active = data.activeAvatarId == cell.def.id;
                if (owned) avOwned++;
                cell.bg.color = active ? Rgba(ColGreen, 0.14f) : Rgba(Color.white, owned ? 0.05f : 0.03f);
                cell.icon.gameObject.SetActive(owned);
                cell.checkBadge.SetActive(active);
                cell.lockBadge.SetActive(!owned);
            }
            _avatarCountLabel.text = $"{avOwned} з {_avatarCells.Count}";

            int frOwned = 0;
            foreach (var cell in _frameCells)
            {
                bool owned = gm.IsFrameOwned(cell.def.id);
                bool active = data.activeFrameId == cell.def.id;
                if (owned) frOwned++;
                cell.ring.color = owned ? Hex(cell.def.colorHex) : Rgba(Color.white, 0.14f);
                cell.checkBadge.SetActive(active);
                cell.lockBadge.SetActive(!owned);
                cell.label.color = active ? Hex(cell.def.colorHex) : ColMut;
            }
            _frameCountLabel.text = $"{frOwned} з {_frameCells.Count}";
        }

        private static Color TierColor(string tier) => tier switch
        {
            "spark" => UIColors.Hex("#E4C77E"),
            "gold" => UIColors.Hex("#E4C77E"),
            "green" => UIColors.Green,
            _ => UIColors.Hex("#5C6653"),
        };

        // ════════════════════════════════════════════════════════════════
        //  ДІЇ
        // ════════════════════════════════════════════════════════════════
        private void OnAvatarTapped(string id)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (!gm.IsAvatarOwned(id))
            {
                var def = ProfileItemCatalog.FindAvatar(id);
                ShowToast(def != null && def.source != null ? "Заблоковано: " + def.source : "Ще не відкрито");
                return;
            }
            gm.SetActiveAvatar(id);
            RefreshAll();
        }

        private void OnFrameTapped(string id)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (!gm.IsFrameOwned(id))
            {
                var def = ProfileItemCatalog.FindFrame(id);
                ShowToast(def != null && def.source != null ? "Заблоковано: " + def.source : "Ще не відкрито");
                return;
            }
            gm.SetActiveFrame(id);
            RefreshAll();
        }

        private void OpenRename()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.playerData.renameTokens <= 0)
            {
                ShowToast("Потрібен токен зміни імені");
                return;
            }
            if (renamePanel == null) return;
            if (renameInput != null) renameInput.text = gm.playerData.playerName;
            renamePanel.SetActive(true);
        }

        private void ConfirmRename()
        {
            var gm = GameManager.Instance;
            if (gm == null || renameInput == null) return;
            if (gm.TryRename(renameInput.text))
            {
                renamePanel.SetActive(false);
                RefreshAll();
            }
            else
            {
                ShowToast("Введи ім'я");
            }
        }

        private void ShowToast(string message)
        {
            if (toastLabel != null) toastLabel.text = message;
            if (toastRoot != null) toastRoot.SetActive(true);
            _toastTimer = 1.8f;
        }

        // ════════════════════════════════════════════════════════════════
        //  ХЕЛПЕРИ ПОБУДОВИ (копії, як у DexScreenController/LabScreenController)
        // ════════════════════════════════════════════════════════════════
        private GameObject MakeRow(Transform parent, float height, float spacing)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            return row;
        }

        private Button MakeActionButton(Transform parent, string label, bool small = false)
        {
            var go = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = small ? 78 : 118; le.preferredHeight = small ? 28 : 38;
            var img = go.GetComponent<Image>();
            img.sprite = sprRoundedSmall; img.type = Image.Type.Sliced;
            img.color = Rgba(Color.white, 0.06f);
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var lbl = MakeLabel(go.transform, "Label", label, fontUi, small ? 11.5f : 13, Hex("#C9CFC1"), FontStyles.Bold);
            Stretch(lbl.rectTransform);
            lbl.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private TMP_Text MakePill(Transform parent, string text, Color textColor, Color bgColor, Color borderColor)
        {
            var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            pill.transform.SetParent(parent, false);
            var img = pill.GetComponent<Image>();
            img.sprite = sprPillTiny; img.type = Image.Type.Sliced; img.color = bgColor;
            var hl = pill.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(13, 13, 5, 5); hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            pill.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var border = MakeImage(pill.transform, "Border", sprPillTiny, borderColor, Image.Type.Sliced);
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var label = MakeLabel(pill.transform, "Label", text, fontUi, 13, textColor, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        private Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = color; img.type = type;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI MakeLabel(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (font != null) tmp.font = font;
            tmp.fontSize = size; tmp.color = color; tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static readonly Color ColText = UIColors.Text;
        private static readonly Color ColMut = UIColors.Hex("#8C9683");
        private static readonly Color ColGreen = UIColors.Green;
        private static readonly Color ColGreenDeep = UIColors.Hex("#8FBF5A");
        private static readonly Color ColGold = UIColors.Hex("#C9A65A");
        private static readonly Color ColGoldLt = UIColors.Hex("#E4C77E");
        private static readonly Color ColViolet = UIColors.Hex("#B9A6E6");

        private static Color Hex(string hex) => UIColors.Hex(hex);
        private static Color Rgba(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
