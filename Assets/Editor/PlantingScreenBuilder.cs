using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.UI;
using static PlantBreeding.EditorTools.UIBuilderKit;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Збирає статичний каркас сцени "Посадка" за дизайном з папки
    /// "Посадка/export/planting-screen.html": шапка, три каруселі вибору
    /// (рослина/горщик/добриво), панель прогнозу + кнопка "Посадити".
    /// Наповнення карток даними — рантайм-скрипт PlantingScreenController.
    /// Сцена завантажується завжди additive поверх MainGarden (без власних
    /// Camera/Light/EventSystem — бере їх з уже завантаженої сцени саду).
    /// Меню: PlantBreeding → Зібрати екран «Посадка».
    /// </summary>
    public static class PlantingScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/Planting.unity";
        const float W = 390f, H = 844f;

        [MenuItem("PlantBreeding/Зібрати екран «Посадка»")]
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
            BuildScene();
            AddSceneToBuildSettingsAdditive();
            Debug.Log("[PlantBreeding] Екран «Посадка» зібрано → " + ScenePath);
        }

        [MenuItem("PlantBreeding/Зібрати все (контент + сад + посадка + лабораторія + дендрарій + профіль)")]
        public static void BuildAll()
        {
            ContentSeeder.Seed();
            HomeScreenBuilder.Build();
            Build();
            LabScreenBuilder.Build();
            DexScreenBuilder.Build();
            ShopScreenBuilder.Build();
            ProfileScreenBuilder.Build();
            Debug.Log("[PlantBreeding] Усе зібрано: контент, сад, посадка, лабораторія, дендрарій, крамниця, профіль.");
        }

        static void AddSceneToBuildSettingsAdditive()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void BuildScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Без камери/світла/EventSystem — сцена завжди довантажується (additive)
            // поверх MainGarden, яка вже їх має.
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // поверх Canvas сцени саду
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGO.transform;

            var bg = MakeImage("Background", root, LoadSprite("bg-gradient"), Color.white, Image.Type.Simple);
            Stretch(bg.rectTransform);
            var glow = MakeImage("GlowTopRight", root, LoadSprite("glow"), Rgba(ColGreen, 0.16f), Image.Type.Simple);
            Place(glow.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-70, -50), new Vector2(220, 220));

            var controller = canvasGO.AddComponent<PlantingScreenController>();
            controller.fontHead = FontHead;
            controller.fontUi = FontUi;
            controller.sprRoundedCard = LoadSprite("rounded-20");
            controller.sprRoundedCardLine = LoadSprite("rounded-20-line");
            controller.sprPill = LoadSprite("pill-16");
            controller.sprPillLine = LoadSprite("pill-16-line");
            controller.sprPotGlyph = LoadSprite("pot-glyph");

            // Єдина шапка всіх екранів (HeaderView) + «назад»; монети/кристали
            // оновлює HeaderView, тож контролеру їх не передаємо.
            BuildStandardHeader(root, withBack: true, out var backButton);
            controller.backButton = backButton;
            BuildTitle(root);

            var scrollContent = BuildScrollArea(root);
            controller.plantsContent = BuildCarouselSection(scrollContent, "1", "Оберіть рослину", "інвентар", 140, "green");
            controller.potsContent = BuildCarouselSection(scrollContent, "2", "Оберіть горщик", "останній обраний", 140, "green");
            controller.boostsContent = BuildCarouselSection(scrollContent, "3", "Добриво", "опційно", 80, "mut");

            BuildPreviewBar(root, controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ── Заголовок під шапкою (позиція як «Мій сад») ──────────────────
        static void BuildTitle(Transform root)
        {
            var title = MakeLabel("Title", root, "Посадка", FontHead, 36, ColText, FontStyles.Normal);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -122), new Vector2(280, 40));
            title.alignment = TextAlignmentOptions.BottomLeft;
        }

        // ── Скролований контейнер під три секції ─────────────────────────
        static Transform BuildScrollArea(Transform root)
        {
            var scrollGO = new GameObject("ScrollArea", typeof(RectTransform), typeof(ScrollRect));
            scrollGO.transform.SetParent(root, false);
            var srt = (RectTransform)scrollGO.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(0, 216);  // над PreviewBar
            srt.offsetMax = new Vector2(0, -180); // під шапкою (той самий відступ, що й Grid на екрані саду)

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(scrollGO.transform, false);
            Stretch((RectTransform)viewport.transform);
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.001f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0, 0);
            var vl = content.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 6; vl.padding = new RectOffset(20, 20, 4, 20);
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGO.GetComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.viewport = (RectTransform)viewport.transform;
            sr.content = crt;
            sr.movementType = ScrollRect.MovementType.Clamped;

            return content.transform;
        }

        // ── Одна секція-карусель: заголовок + горизонтальний скрол ───────
        static Transform BuildCarouselSection(Transform parent, string index, string title, string hint, float rowHeight, string accent)
        {
            var section = new GameObject("Section_" + title, typeof(RectTransform), typeof(LayoutElement));
            section.transform.SetParent(parent, false);
            section.GetComponent<LayoutElement>().preferredHeight = 34 + 8 + rowHeight;
            section.GetComponent<LayoutElement>().minHeight = 34 + 8 + rowHeight;

            var headerRow = new GameObject("HeaderRow", typeof(RectTransform));
            headerRow.transform.SetParent(section.transform, false);
            var hrrt = (RectTransform)headerRow.transform;
            hrrt.anchorMin = new Vector2(0, 1); hrrt.anchorMax = new Vector2(1, 1);
            hrrt.pivot = new Vector2(0.5f, 1);
            hrrt.anchoredPosition = Vector2.zero;
            hrrt.sizeDelta = new Vector2(0, 26);

            var badgeBg = accent == "green" ? Rgba(ColGreen, 0.2f) : Rgba(Hex("#8C9683"), 0.18f);
            var badgeFg = accent == "green" ? ColChipOk : Hex("#B4BEA8");
            var badge = MakeImage("Badge", headerRow.transform, LoadSprite("rounded-12"), badgeBg, Image.Type.Sliced);
            Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(22, 22));
            var badgeLbl = MakeLabel("Num", badge.transform, index, FontUi, 12, badgeFg, FontStyles.Bold);
            Stretch(badgeLbl.rectTransform);
            badgeLbl.alignment = TextAlignmentOptions.Center;

            var titleLbl = MakeLabel("Title", headerRow.transform, title, FontHead, 20, ColText, FontStyles.Normal);
            Place(titleLbl.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(31, 0), new Vector2(160, 26));
            titleLbl.alignment = TextAlignmentOptions.MidlineLeft;

            var hintLbl = MakeLabel("Hint", headerRow.transform, "· " + hint, FontUi, 11, ColTextMut, FontStyles.Bold);
            Place(hintLbl.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(195, 0), new Vector2(150, 16));
            hintLbl.alignment = TextAlignmentOptions.MidlineLeft;

            var scrollGO = new GameObject("Row", typeof(RectTransform), typeof(ScrollRect));
            scrollGO.transform.SetParent(section.transform, false);
            var srt = (RectTransform)scrollGO.transform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1);
            srt.offsetMin = new Vector2(-20, 0); srt.offsetMax = new Vector2(20, -34);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(scrollGO.transform, false);
            Stretch((RectTransform)viewport.transform);
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.001f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(0, 1);
            crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0, 0); // без цього дефолтний RectTransform (100×100) додається до розтягнутої висоти
            var hl = content.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 11; hl.padding = new RectOffset(20, 20, 2, 2);
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGO.GetComponent<ScrollRect>();
            sr.horizontal = true; sr.vertical = false;
            sr.viewport = (RectTransform)viewport.transform;
            sr.content = crt;
            sr.movementType = ScrollRect.MovementType.Clamped;

            return content.transform;
        }

        // ── Прогноз + кнопка "Посадити" ───────────────────────────────────
        static void BuildPreviewBar(Transform root, PlantingScreenController controller)
        {
            var bar = new GameObject("PreviewBar", typeof(RectTransform));
            bar.transform.SetParent(root, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.offsetMin = new Vector2(16, 20); brt.offsetMax = new Vector2(-16, 200);

            var card = MakeImage("Card", bar.transform, LoadSprite("rounded-24"), new Color(0.051f, 0.071f, 0.039f, 0.80f), Image.Type.Sliced);
            var crt = card.rectTransform;
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0, 82);
            var cardLine = MakeImage("Border", card.transform, LoadSprite("rounded-24-line"), Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(cardLine.rectTransform);

            var potBox = MakeImage("PotBox", card.transform, LoadSprite("rounded-16"), Rgba(ColGreen, 0.10f), Image.Type.Sliced);
            Place(potBox.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(13, 0), new Vector2(56, 56));
            var potBoxLine = MakeImage("Border", potBox.transform, LoadSprite("rounded-16-line"), Rgba(ColGreen, 0.22f), Image.Type.Sliced);
            Stretch(potBoxLine.rectTransform);
            var potGlyph = MakeImage("PotGlyph", potBox.transform, LoadSprite("pot-glyph"), ColGreen, Image.Type.Simple);
            Place(potGlyph.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 4), new Vector2(44, 34));
            var plantIcon = MakeImage("PlantIcon", potBox.transform, LoadSprite("plant-stage-2", SvgDir), Color.white, Image.Type.Simple);
            plantIcon.preserveAspect = true;
            Place(plantIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.35f), new Vector2(0, 0), new Vector2(30, 30));
            plantIcon.enabled = false;

            var forecastPlaceholder = MakeLabel("Placeholder", card.transform, "Оберіть рослину для прогнозу", FontUi, 13, ColTextMut, FontStyles.Normal);
            Place(forecastPlaceholder.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(82, 0), new Vector2(260, 20));
            forecastPlaceholder.alignment = TextAlignmentOptions.MidlineLeft;

            var columns = new GameObject("ForecastColumns", typeof(RectTransform));
            columns.transform.SetParent(card.transform, false);
            Place((RectTransform)columns.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(82, 0), new Vector2(260, 60));
            var chl = columns.AddComponent<HorizontalLayoutGroup>();
            chl.spacing = 16; chl.childAlignment = TextAnchor.MiddleLeft;
            // Обидва control=true + forceExpand=false — інакше LayoutElement-розміри
            // ігноруються: Divider ставав 100px завширшки (колонка «монет» з'їжджала
            // праворуч), а колонки — 0px заввишки (текст вилазив під картку).
            chl.childControlWidth = true; chl.childControlHeight = true;
            chl.childForceExpandWidth = false; chl.childForceExpandHeight = false;

            var (growLbl, growNote) = BuildForecastColumn(columns.transform, "год", ColText, 100);
            var div = MakeImage("Divider", columns.transform, null, Rgba(Color.white, 0.12f), Image.Type.Simple);
            var divLe = div.gameObject.AddComponent<LayoutElement>();
            divLe.preferredWidth = 1; divLe.preferredHeight = 44;
            var (sellLbl, sellNote) = BuildForecastColumn(columns.transform, "монет", ColGoldLt, 100);

            var plantBtn = MakeImage("PlantButton", bar.transform, LoadSprite("rounded-16"), ColGreenBr, Image.Type.Sliced);
            plantBtn.raycastTarget = true; // MakeImage вимикає raycastTarget за замовчуванням (декоративні елементи) —
                                            // це фон Button.targetGraphic, тому саме він має ловити клік
            var pbrt = plantBtn.rectTransform;
            pbrt.anchorMin = new Vector2(0, 0); pbrt.anchorMax = new Vector2(1, 0);
            pbrt.pivot = new Vector2(0.5f, 0);
            pbrt.anchoredPosition = new Vector2(0, 0);
            pbrt.sizeDelta = new Vector2(0, 52);
            var plantBtnComp = plantBtn.gameObject.AddComponent<Button>();
            plantBtnComp.targetGraphic = plantBtn;
            var plantBtnLbl = MakeLabel("Label", plantBtn.transform, "Оберіть рослину", FontUi, 15, Hex("#0E130C"), FontStyles.Bold);
            Stretch(plantBtnLbl.rectTransform);
            plantBtnLbl.alignment = TextAlignmentOptions.Center;

            controller.previewPotGlyph = potGlyph;
            controller.previewPlantImage = plantIcon;
            controller.forecastPlaceholder = forecastPlaceholder;
            controller.forecastColumns = columns.transform;
            controller.growLabel = growLbl;
            controller.growNoteLabel = growNote;
            controller.sellLabel = sellLbl;
            controller.sellNoteLabel = sellNote;
            controller.plantButton = plantBtnComp;
            controller.plantButtonLabel = plantBtnLbl;
        }

        static (TMP_Text value, TMP_Text note) BuildForecastColumn(Transform parent, string unit, Color valueColor, float width)
        {
            var col = new GameObject("Col_" + unit, typeof(RectTransform), typeof(LayoutElement));
            col.transform.SetParent(parent, false);
            var colLe = col.GetComponent<LayoutElement>();
            colLe.preferredWidth = width;
            colLe.preferredHeight = 60; // Value 26 + Unit 16 + Note 14 (див. офсети нижче)

            // Офсети рахуються від висоти елемента ВИЩЕ (26/16/14), інакше великий
            // Value-текст (22pt) заходить на Unit під ним — саме це й "з'їжджало".
            var value = MakeLabel("Value", col.transform, "0:00", FontHead, 22, valueColor, FontStyles.Bold);
            Place(value.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(width, 26));
            value.alignment = TextAlignmentOptions.TopLeft;
            value.textWrappingMode = TextWrappingModes.NoWrap;

            var unitLbl = MakeLabel("Unit", col.transform, unit, FontUi, 11, ColSoft, FontStyles.Normal);
            Place(unitLbl.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -28), new Vector2(width, 16));
            unitLbl.alignment = TextAlignmentOptions.TopLeft;
            unitLbl.textWrappingMode = TextWrappingModes.NoWrap;

            var note = MakeLabel("Note", col.transform, "базовий", FontUi, 9.5f, ColGreen, FontStyles.Normal);
            Place(note.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -46), new Vector2(width, 14));
            note.alignment = TextAlignmentOptions.TopLeft;
            note.textWrappingMode = TextWrappingModes.NoWrap;

            return (value, note);
        }
    }
}
