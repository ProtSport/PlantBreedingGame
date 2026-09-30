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
    /// Збирає статичний каркас сцени «Дендрарій» за дизайном з папки
    /// Дендрарій/export/dex-collections.html: шапка (той самий уніфікований
    /// header, що на «Мій сад»/«Лабораторія» — рядок валют y=-44, kicker y=-104,
    /// title y=-122, БЕЗ кнопки «назад», бо це пункт нижнього меню, а не
    /// вкладений під-екран), скролований список колекцій, деталь-оверлей
    /// (розкрита колекція, ховається за замовчуванням), нижнє меню.
    /// Наповнення карток — рантайм-скрипт DexScreenController.
    /// Сцена завантажується additive поверх MainGarden (без Camera/Light/
    /// EventSystem), як «Посадка»/«Лабораторія».
    /// Меню: PlantBreeding → Зібрати екран «Дендрарій».
    /// </summary>
    public static class DexScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/Dex.unity";
        const float W = 390f, H = 844f;

        [MenuItem("PlantBreeding/Зібрати екран «Дендрарій»")]
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
            Debug.Log("[PlantBreeding] Екран «Дендрарій» зібрано → " + ScenePath);
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

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGO.transform;

            var bg = MakeImage("Background", root, LoadSprite("bg-gradient"), Color.white, Image.Type.Simple);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true; // ловить кліки, щоб не «протикати» у сцену саду під нею
            var glow = MakeImage("GlowTopRight", root, LoadSprite("glow"), Rgba(ColGoldLt, 0.14f), Image.Type.Simple);
            Place(glow.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-70, -50), new Vector2(220, 220));

            var controller = canvasGO.AddComponent<DexScreenController>();
            controller.fontHead = FontHead;
            controller.fontUi = FontUi;
            controller.sprRoundedCard = LoadSprite("rounded-20");
            controller.sprRoundedCardLine = LoadSprite("rounded-20-line");
            controller.sprRoundedSmall = LoadSprite("rounded-16");
            controller.sprRoundedSmallLine = LoadSprite("rounded-16-line");
            controller.sprPillTiny = LoadSprite("pill-10");
            controller.sprSprig = LoadSprite("sprig");
            controller.sprIconCheck = LoadSprite("icon-check");
            controller.sprGlow = LoadSprite("glow");

            // Єдина шапка всіх екранів (HeaderView); монети/кристали оновлює вона.
            BuildStandardHeader(root, withBack: false, out _);
            BuildTitle(root);

            controller.listContent = BuildScrollArea(root, 118, -168);

            BuildBottomNav(root);
            BuildDetailPanel(root, controller);
            BuildToast(root, controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ── Заголовок під шапкою (позиція як «Мій сад») ──────────────────
        static void BuildTitle(Transform root)
        {
            var title = MakeLabel("Title", root, "Колекції", FontHead, 36, ColText, FontStyles.Normal);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -122), new Vector2(280, 40));
            title.alignment = TextAlignmentOptions.BottomLeft;
        }

        // ── Скролований вертикальний список ───────────────────────────────
        static Transform BuildScrollArea(Transform parent, float bottomOffset, float topOffset)
        {
            var scrollGO = new GameObject("ScrollArea", typeof(RectTransform), typeof(ScrollRect));
            scrollGO.transform.SetParent(parent, false);
            var srt = (RectTransform)scrollGO.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(0, bottomOffset);
            srt.offsetMax = new Vector2(0, topOffset);

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
            vl.spacing = 13; vl.padding = new RectOffset(16, 16, 8, 24);
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

        // ── Деталь-оверлей (розкрита колекція) ────────────────────────────
        static void BuildDetailPanel(Transform root, DexScreenController controller)
        {
            var panel = new GameObject("DetailPanel", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            Stretch((RectTransform)panel.transform);

            var pbg = MakeImage("Bg", panel.transform, LoadSprite("bg-gradient"), Color.white, Image.Type.Simple);
            Stretch(pbg.rectTransform);
            pbg.raycastTarget = true;
            var pglow = MakeImage("Glow", panel.transform, LoadSprite("glow"), Rgba(ColGoldLt, 0.16f), Image.Type.Simple);
            Place(pglow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(300, 200));

            // Кнопка «назад»
            var backGo = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backGo.transform.SetParent(panel.transform, false);
            var backImg = backGo.GetComponent<Image>();
            backImg.sprite = LoadSprite("rounded-12"); backImg.type = Image.Type.Sliced;
            backImg.color = Rgba(Color.white, 0.055f); backImg.raycastTarget = true;
            Place(backImg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -52), new Vector2(42, 42));
            var backLine = MakeImage("Border", backGo.transform, LoadSprite("rounded-12-line"), Rgba(Color.white, 0.10f), Image.Type.Sliced);
            Stretch(backLine.rectTransform);
            var chevron = MakeImage("Chevron", backGo.transform, LoadSprite("chevron-left"), Hex("#C9CFC1"), Image.Type.Simple);
            chevron.preserveAspect = true;
            Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(19, 19));
            controller.detailBackButton = backGo.GetComponent<Button>();
            controller.detailBackButton.targetGraphic = backImg;

            var kicker = MakeLabel("DetailKicker", panel.transform, "", FontUi, 11, ColTextMut, FontStyles.Bold);
            kicker.characterSpacing = 8;
            Place(kicker.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(74, -46), new Vector2(230, 14));
            kicker.alignment = TextAlignmentOptions.BottomLeft;
            controller.detailKicker = kicker;

            var title = MakeLabel("DetailTitle", panel.transform, "", FontHead, 28, ColText, FontStyles.Normal);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(74, -64), new Vector2(230, 34));
            title.alignment = TextAlignmentOptions.BottomLeft;
            controller.detailTitle = title;

            var count = MakeLabel("DetailCount", panel.transform, "", FontHead, 26, ColGold, FontStyles.Normal);
            Place(count.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -56), new Vector2(90, 30));
            count.alignment = TextAlignmentOptions.MidlineRight;
            controller.detailCount = count;

            controller.detailContent = BuildScrollArea(panel.transform, 96, -104);

            // Claim-бар (внизу)
            var claimGo = new GameObject("ClaimBar", typeof(RectTransform), typeof(Image), typeof(Button));
            claimGo.transform.SetParent(panel.transform, false);
            var claimRt = (RectTransform)claimGo.transform;
            claimRt.anchorMin = new Vector2(0, 0); claimRt.anchorMax = new Vector2(1, 0);
            claimRt.pivot = new Vector2(0.5f, 0);
            claimRt.offsetMin = new Vector2(18, 24); claimRt.offsetMax = new Vector2(-18, 78);
            var claimImg = claimGo.GetComponent<Image>();
            claimImg.sprite = LoadSprite("rounded-16"); claimImg.type = Image.Type.Sliced;
            claimImg.color = Rgba(Color.white, 0.05f); claimImg.raycastTarget = true;
            var claimLabel = MakeLabel("Label", claimGo.transform, "", FontUi, 15, Hex("#7C8573"), FontStyles.Bold);
            Stretch(claimLabel.rectTransform);
            claimLabel.alignment = TextAlignmentOptions.Center;
            controller.detailClaimBg = claimImg;
            controller.detailClaimButton = claimGo.GetComponent<Button>();
            controller.detailClaimButton.targetGraphic = claimImg;
            controller.detailClaimLabel = claimLabel;

            controller.detailPanel = panel;
            panel.SetActive(false);
        }

        // ── Тост ──────────────────────────────────────────────────────────
        static void BuildToast(Transform root, DexScreenController controller)
        {
            var toast = MakeImage("Toast", root, LoadSprite("rounded-12"), new Color(0.08f, 0.09f, 0.06f, 0.95f), Image.Type.Sliced);
            var rt = toast.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -174);
            rt.sizeDelta = new Vector2(300, 40);
            var line = MakeImage("Border", toast.transform, LoadSprite("rounded-12-line"), Rgba(Color.white, 0.14f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            var label = MakeLabel("Label", toast.transform, "", FontUi, 12.5f, Hex("#EBE4D6"), FontStyles.Bold);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;

            toast.transform.SetAsLastSibling();
            toast.gameObject.SetActive(false);
            controller.toastRoot = toast.gameObject;
            controller.toastLabel = label;
        }

        // ── Нижнє меню (активний пункт «Дендрарій», «Сад» закриває сцену) ──
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

            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-garden", "Сад", false, "Dex", SceneNavButton.NavAction.CloseScene);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-lab", "Лабораторія", false, "Lab", SceneNavButton.NavAction.OpenAdditive);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-dex", "Дендрарій", true, null, SceneNavButton.NavAction.OpenAdditive);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-shop", "Крамниця", false, SceneNavButton.ShopScene, SceneNavButton.NavAction.OpenAdditive);
        }
    }
}
