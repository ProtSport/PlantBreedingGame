using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Garden;
using PlantBreeding.UI;
using static PlantBreeding.EditorTools.UIBuilderKit;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Збирає перший екран "Мій сад" за дизайном з папки
    /// "Мобільна гра розведення рослин (1)/export/home-screen.html", але з
    /// РЕАЛЬНОЮ сіткою (GardenManager + PlotSlot.prefab) замість мокапу:
    /// перші 4 слоти Empty (тап відкриває сцену "Посадка"), решта Locked.
    /// Меню: PlantBreeding → Зібрати екран «Мій сад».
    /// </summary>
    public static class HomeScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/MainGarden.unity";
        const string PrefabDir = "Assets/Prefabs";
        const string PrefabPath = PrefabDir + "/PlotSlot.prefab";

        const float W = 390f, H = 844f;

        [MenuItem("PlantBreeding/Зібрати екран «Мій сад»")]
        public static void Build()
        {
            if (TMP_Settings.instance == null)
            {
                EditorUtility.DisplayDialog(
                    "Потрібні TMP Essentials",
                    "Спочатку імпортуй ресурси TextMeshPro:\nWindow → TextMeshPro → Import TMP Essential Resources,\nпотім запусти цей пункт меню ще раз.",
                    "OK");
                return;
            }

            EnsureGenerated();
            BuildPlotPrefab();
            BuildScene();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AddSceneToBuildSettings(ScenePath);
            Debug.Log("[PlantBreeding] Екран «Мій сад» зібрано → " + ScenePath);
        }

        internal static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ════════════════════════════════════════════════════════════════
        //  СЦЕНА
        // ════════════════════════════════════════════════════════════════
        static void BuildScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#0E130C");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0, 0, -10);

            var lightGO = new GameObject("Directional Light", typeof(Light));
            lightGO.GetComponent<Light>().type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

            var gmGO = new GameObject("GameManager", typeof(GameManager));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;

            var esGO = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif

            var root = canvasGO.transform;

            var bg = MakeImage("Background", root, LoadSprite("bg-gradient"), Color.white, Image.Type.Simple);
            Stretch(bg.rectTransform);

            var glowTR = MakeImage("GlowTopRight", root, LoadSprite("glow"), Rgba(ColGreen, 0.18f), Image.Type.Simple);
            Place(glowTR.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-70, -50), new Vector2(220, 220));

            BuildStandardHeader(root, withBack: false, out _);
            var statsLabels = BuildTitleAndStats(root);
            var gardenManager = BuildGrid(root, gmGO);
            BuildBottomNav(root);

            var statsView = canvasGO.AddComponent<GardenStatsView>();
            statsView.manager = gardenManager;
            statsView.growingCountLabel = statsLabels.growing;
            statsView.readyCountLabel = statsLabels.ready;

            // Економіка: тости, вікна «Завдання дня»/Крамниця/прискорення/лікування
            // (відкриваються з шапки HeaderView будь-якого екрану), «Зібрати все».
            var economy = canvasGO.AddComponent<EconomyOverlayView>();
            economy.garden = gardenManager; // кнопка «Зібрати все»
            economy.shopNavItem = root.Find("BottomNav/Nav_Крамниця") as RectTransform;
            economy.dexNavItem = root.Find("BottomNav/Nav_Дендрарій") as RectTransform; // бейдж нових рослин
            economy.sprRounded = LoadSprite("rounded-16");
            economy.sprRoundedLine = LoadSprite("rounded-16-line");
            economy.sprPill = LoadSprite("pill-16");
            economy.sprPillLine = LoadSprite("pill-16-line");
            economy.sprCircle = LoadSprite("circle-fill");
            economy.fontHead = FontHead;
            economy.fontUi = FontUi;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        struct StatLabels { public TMP_Text growing, ready; }

        static Sprite LoadAilmentSprite(string name)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Ailments/{name}.svg");
            if (sp == null) Debug.LogWarning($"[PlantBreeding] Оверлей хвороби не знайдено: Assets/Art/Ailments/{name}.svg");
            return sp;
        }

        // ── Під шапкою: заголовок «Мій сад» і лічильники ростуть/готові ──
        static StatLabels BuildTitleAndStats(Transform root)
        {
            var title = MakeLabel("Title", root, "Мій сад", FontHead, 36, ColText, FontStyles.Normal);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -122), new Vector2(220, 40));
            title.alignment = TextAlignmentOptions.BottomLeft;

            var stats = new GameObject("Stats", typeof(RectTransform));
            stats.transform.SetParent(root, false);
            var stRt = (RectTransform)stats.transform;
            stRt.anchorMin = stRt.anchorMax = new Vector2(1, 1);
            stRt.pivot = new Vector2(1, 1);
            stRt.anchoredPosition = new Vector2(-20, -118);
            stRt.sizeDelta = new Vector2(150, 44);
            var shl = stats.AddComponent<HorizontalLayoutGroup>();
            shl.spacing = 16; shl.childAlignment = TextAnchor.MiddleRight;
            shl.childControlWidth = true; shl.childControlHeight = true;
            shl.childForceExpandWidth = false; shl.childForceExpandHeight = true;

            var growingLabel = MakeStat(stats.transform, "0", "ростуть", ColGreen);
            var div = MakeImage("Divider", stats.transform, null, Rgba(Color.white, 0.12f), Image.Type.Simple);
            div.gameObject.AddComponent<LayoutElement>().preferredWidth = 1;
            var readyLabel = MakeStat(stats.transform, "0", "готові", ColGold);

            return new StatLabels { growing = growingLabel, ready = readyLabel };
        }

        static TMP_Text MakeStat(Transform parent, string num, string label, Color numColor)
        {
            var col = new GameObject("Stat_" + label, typeof(RectTransform));
            col.transform.SetParent(parent, false);
            var vl = col.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 2; vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = false; vl.childForceExpandHeight = false;

            var n = MakeLabel("Num", col.transform, num, FontHead, 26, numColor, FontStyles.Normal);
            n.alignment = TextAlignmentOptions.Center;
            var l = MakeLabel("Label", col.transform, label, FontUi, 13, ColTextMut, FontStyles.Normal);
            l.alignment = TextAlignmentOptions.Center;
            return n;
        }

        // ── Сітка грядок 2 × 3 (реальна, керована GardenManager) ─────────
        static GardenManager BuildGrid(Transform root, GameObject gmGO)
        {
            var grid = new GameObject("Grid", typeof(RectTransform));
            grid.transform.SetParent(root, false);
            var rt = (RectTransform)grid.transform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(16, 118);
            rt.offsetMax = new Vector2(-16, -190);

            var gl = grid.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(173, 170);
            gl.spacing = new Vector2(12, 12);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            gl.childAlignment = TextAnchor.UpperCenter;

            var prefab = AssetDatabase.LoadAssetAtPath<PlotSlotView>(PrefabPath);
            var gardenManager = gmGO.AddComponent<GardenManager>();
            gardenManager.gridSize = 6;
            gardenManager.initiallyUnlocked = 4;
            gardenManager.plotViewPrefab = prefab;
            gardenManager.gridParent = grid.transform;
            return gardenManager;
        }

        // ── Нижнє меню (скляна панель) ───────────────────────────────────
        static void BuildBottomNav(Transform root)
        {
            var nav = MakeImage("BottomNav", root, LoadSprite("rounded-24"), new Color(0.051f, 0.071f, 0.039f, 0.90f), Image.Type.Sliced);
            var rt = nav.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(16, 14);
            rt.offsetMax = new Vector2(-16, 100);

            var line = MakeImage("Border", nav.transform, LoadSprite("rounded-24-line"), Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var hl = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(6, 6, 8, 8);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;

            MakeNavItem(nav.transform, "nav-garden", "Сад", true, null, SceneNavButton.NavAction.OpenAdditive);
            MakeNavItem(nav.transform, "nav-lab", "Лабораторія", false, "Lab", SceneNavButton.NavAction.OpenAdditive);
            MakeNavItem(nav.transform, "nav-dex", "Дендрарій", false, "Dex", SceneNavButton.NavAction.OpenAdditive);
            MakeNavItem(nav.transform, "nav-shop", "Крамниця", false, null, SceneNavButton.NavAction.OpenAdditive);
        }

        /// <summary>
        /// Спільний пункт нижнього меню — використовує і "Мій сад", і
        /// "Лабораторія" (LabScreenBuilder), щоб панель виглядала однаково
        /// на всіх екранах. navSceneName == null → пункт лише декоративний
        /// (екран ще не реалізовано). Клік підключається РАНТАЙМ-компонентом
        /// SceneNavButton (Start()), а не тут — AddListener в Editor-коді
        /// білдера не переживає вхід у Play-режим (не серіалізується).
        /// </summary>
        internal static void MakeNavItem(Transform parent, string icon, string label, bool active,
            string navSceneName, SceneNavButton.NavAction navAction)
        {
            var item = new GameObject("Nav_" + label, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var vl = item.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 5; vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = false; vl.childControlHeight = false;
            vl.childForceExpandWidth = false; vl.childForceExpandHeight = false;

            if (navSceneName != null)
            {
                var btn = item.AddComponent<Button>();
                var hitArea = item.AddComponent<Image>();
                hitArea.color = new Color(0, 0, 0, 0.001f); // прозора, але raycastTarget=true за замовчуванням — ловить клік по всьому пункту
                btn.targetGraphic = hitArea;
                var nav = item.AddComponent<SceneNavButton>();
                nav.action = navAction;
                nav.sceneName = navSceneName;
            }

            var box = new GameObject("IconBox", typeof(RectTransform));
            box.transform.SetParent(item.transform, false);
            ((RectTransform)box.transform).sizeDelta = new Vector2(44, 38);
            if (active)
            {
                var bg = MakeImage("ActiveBg", box.transform, LoadSprite("rounded-12"), Rgba(ColGreen, 0.18f), Image.Type.Sliced);
                Stretch(bg.rectTransform);
                var bLine = MakeImage("Border", box.transform, LoadSprite("rounded-12-line"), Rgba(ColGreen, 0.34f), Image.Type.Sliced);
                Stretch(bLine.rectTransform);
            }
            var ico = MakeImage("Icon", box.transform, LoadSprite(icon, SvgDir), Color.white, Image.Type.Simple);
            ico.preserveAspect = true;
            Place(ico.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));

            var txt = MakeLabel("Label", item.transform, label, FontUi, 11,
                active ? ColChipOk : ColTextMut, FontStyles.Bold);
            ((RectTransform)txt.transform).sizeDelta = new Vector2(90, 14);
            txt.alignment = TextAlignmentOptions.Center;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // ════════════════════════════════════════════════════════════════
        //  ПРЕФАБ ПЛИТКИ ГРЯДКИ (PlotSlot.prefab)
        // ════════════════════════════════════════════════════════════════
        static void BuildPlotPrefab()
        {
            Directory.CreateDirectory(PrefabDir);

            var tile = new GameObject("PlotSlot", typeof(RectTransform), typeof(Image), typeof(Button));
            var tileImg = tile.GetComponent<Image>();
            tileImg.sprite = LoadSprite("rounded-24");
            tileImg.type = Image.Type.Sliced;
            tileImg.color = Rgba(Color.white, 0.05f);
            var tileRt = (RectTransform)tile.transform;
            tileRt.sizeDelta = new Vector2(173, 170);
            tile.AddComponent<RectMask2D>();

            var border = MakeImage("Border", tile.transform, LoadSprite("rounded-24-line"), Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(border.rectTransform);

            var glow = MakeImage("Glow", tile.transform, LoadSprite("glow"), Rgba(ColGreen, 0.22f), Image.Type.Simple);
            var grt = glow.rectTransform;
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0);
            grt.pivot = new Vector2(0.5f, 0.5f);
            grt.anchoredPosition = new Vector2(0, 10);
            grt.sizeDelta = new Vector2(200, 130);
            glow.gameObject.SetActive(false);

            // ── EmptyGroup: "+" Посадити ──────────────────────────────────
            var emptyGroup = new GameObject("EmptyGroup", typeof(RectTransform));
            emptyGroup.transform.SetParent(tile.transform, false);
            Stretch((RectTransform)emptyGroup.transform);

            var circle = MakeImage("PlusCircle", emptyGroup.transform, LoadSprite("circle-fill"), Rgba(ColGreen, 0.14f), Image.Type.Simple);
            Place(circle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(40, 40));
            var cLine = MakeImage("Ring", circle.transform, LoadSprite("circle-line"), Rgba(ColGreen, 0.32f), Image.Type.Simple);
            Stretch(cLine.rectTransform);
            var plus = MakeLabel("Plus", circle.transform, "+", FontUi, 22, ColGreen, FontStyles.Normal);
            Stretch(plus.rectTransform);
            plus.alignment = TextAlignmentOptions.Center;
            var emptyLbl = MakeLabel("Label", emptyGroup.transform, "Посадити", FontUi, 12, Hex("#9FAE92"), FontStyles.Bold);
            Place(emptyLbl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(120, 16));
            emptyLbl.alignment = TextAlignmentOptions.Center;

            // ── LockedGroup: 🔒 Заблоковано ───────────────────────────────
            var lockedGroup = new GameObject("LockedGroup", typeof(RectTransform));
            lockedGroup.transform.SetParent(tile.transform, false);
            Stretch((RectTransform)lockedGroup.transform);

            var lockBody = MakeImage("LockBody", lockedGroup.transform, LoadSprite("rounded-12"), Rgba(Color.white, 0.22f), Image.Type.Sliced);
            Place(lockBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(26, 20));
            var lockShackle = MakeImage("LockShackle", lockedGroup.transform, LoadSprite("circle-line"), Rgba(Color.white, 0.30f), Image.Type.Simple);
            Place(lockShackle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(20, 20));
            var lockedLbl = MakeLabel("Label", lockedGroup.transform, "Заблоковано", FontUi, 11, Hex("#6E7A64"), FontStyles.Bold);
            // 2 рядки: "Рівень N" / "або X монет" (EconomyService.LockedPlotLabel).
            Place(lockedLbl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(150, 32));
            lockedLbl.alignment = TextAlignmentOptions.Center;

            // ── ContentGroup: рослина + чіп + назва + прогрес ────────────
            var contentGroup = new GameObject("ContentGroup", typeof(RectTransform));
            contentGroup.transform.SetParent(tile.transform, false);
            Stretch((RectTransform)contentGroup.transform);

            var plant = MakeImage("Plant", contentGroup.transform, LoadSprite("plant-stage-5", SvgDir), Color.white, Image.Type.Simple);
            plant.preserveAspect = true;
            var prt = plant.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0);
            prt.pivot = new Vector2(0.5f, 0);
            prt.anchoredPosition = new Vector2(0, 28);
            prt.sizeDelta = new Vector2(100, 100 * 260f / 200f); // полотно арту 200×260

            // Хвороби: маска по силуету «тільки рослина» + оверлей усередині (тля,
            // роса, хлороз) і окремо павутина кліща. Однакове полотно 200×260 і
            // preserveAspect → маска лягає рівно на рослину.
            var maskImg = MakeImage("AilmentMask", plant.transform, null, Color.white, Image.Type.Simple);
            Stretch(maskImg.rectTransform);
            maskImg.preserveAspect = true;
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var overlayImg = MakeImage("Overlay", maskImg.transform, null, Color.white, Image.Type.Simple);
            Stretch(overlayImg.rectTransform);
            overlayImg.preserveAspect = true;
            maskImg.gameObject.SetActive(false);

            var webImg = MakeImage("Web", plant.transform, LoadAilmentSprite("spider_mite"), Color.white, Image.Type.Simple);
            var wrt = webImg.rectTransform;
            wrt.anchorMin = new Vector2(0.12f, 0.30f); wrt.anchorMax = new Vector2(0.88f, 0.92f); // зона листя над горщиком
            wrt.offsetMin = Vector2.zero; wrt.offsetMax = Vector2.zero;
            webImg.gameObject.SetActive(false);

            var chip = MakeImage("Chip", contentGroup.transform, LoadSprite("pill-10"), Rgba(Color.white, 0.06f), Image.Type.Sliced);
            var crt = chip.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0, 1);
            crt.pivot = new Vector2(0, 1);
            crt.anchoredPosition = new Vector2(10, -10);
            var chl = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            chl.padding = new RectOffset(7, 9, 4, 4);
            chl.spacing = 5; chl.childAlignment = TextAnchor.MiddleLeft;
            chl.childControlWidth = true; chl.childControlHeight = true;
            chl.childForceExpandWidth = false; chl.childForceExpandHeight = false;
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            chip.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var chipBorder = MakeImage("Border", chip.transform, LoadSprite("pill-10-line"), Rgba(Color.white, 0.12f), Image.Type.Sliced);
            Stretch(chipBorder.rectTransform);
            chipBorder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var chipWarn = MakeImage("WarnIcon", chip.transform, LoadSprite("status-warning", SvgDir), Color.white, Image.Type.Simple);
            chipWarn.preserveAspect = true;
            var cwle = chipWarn.gameObject.AddComponent<LayoutElement>();
            cwle.preferredWidth = 11; cwle.preferredHeight = 11;
            chipWarn.gameObject.SetActive(false);

            var chipDot = MakeImage("Dot", chip.transform, LoadSprite("circle-fill"), ColGreenBr, Image.Type.Simple);
            var cdle = chipDot.gameObject.AddComponent<LayoutElement>();
            cdle.preferredWidth = 6; cdle.preferredHeight = 6;

            var chipText = MakeLabel("Text", chip.transform, "Росте", FontUi, 10, ColChipGrow, FontStyles.Bold);
            chipText.alignment = TextAlignmentOptions.MidlineLeft;

            var nameLabel = MakeLabel("Name", contentGroup.transform, "Рослина", FontHead, 18, ColText, FontStyles.Normal);
            var nrt = nameLabel.rectTransform;
            nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 0);
            nrt.pivot = new Vector2(0, 0);
            nrt.anchoredPosition = new Vector2(12, 18);
            nrt.sizeDelta = new Vector2(-24, 20);
            nameLabel.alignment = TextAlignmentOptions.BottomLeft;

            var track = MakeImage("ProgressTrack", contentGroup.transform, LoadSprite("pill-tiny"), Rgba(Color.white, 0.10f), Image.Type.Sliced);
            var trt = track.rectTransform;
            trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 0);
            trt.pivot = new Vector2(0.5f, 0);
            trt.anchoredPosition = new Vector2(0, 9);
            trt.offsetMin = new Vector2(12, 9);
            trt.offsetMax = new Vector2(-12, 13);

            var fill = MakeImage("Fill", track.transform, LoadSprite("pill-tiny"), ColGreen, Image.Type.Sliced);
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0, 0);
            frt.anchorMax = new Vector2(0f, 1);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            // ── Прив'язка PlotSlotView ────────────────────────────────────
            var view = tile.AddComponent<PlotSlotView>();
            view.contentGroup = contentGroup;
            view.emptyGroup = emptyGroup;
            view.lockedGroup = lockedGroup;
            view.background = tileImg;
            view.border = border;
            view.readyGlow = glow;
            view.plantImage = plant;
            view.chipBg = chip;
            view.chipBorder = chipBorder;
            view.chipDot = chipDot;
            view.chipWarningIcon = chipWarn;
            view.chipText = chipText;
            view.nameLabel = nameLabel;
            view.progressFill = fill;
            view.lockedLabel = lockedLbl;
            view.ailmentMask = maskImg;
            view.ailmentOverlay = overlayImg;
            view.webOverlay = webImg;
            view.sprAphids = LoadAilmentSprite("aphids");
            view.sprPowderyMildew = LoadAilmentSprite("powdery_mildew");
            view.sprChlorosis = LoadAilmentSprite("chlorosis");

            var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (oldPrefab != null) AssetDatabase.DeleteAsset(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(tile, PrefabPath);
            Object.DestroyImmediate(tile);
        }
    }
}
