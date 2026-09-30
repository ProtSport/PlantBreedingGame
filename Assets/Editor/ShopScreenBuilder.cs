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
    /// Збирає каркас сцени «Крамниця» за дизайном з папки
    /// Крамниця/export-shop/kramnytsia.html: уніфікована шапка (без «назад» —
    /// це вкладка нижнього меню), заголовок, смуга вкладок-чіпів, скролований
    /// список, нижнє меню з активним пунктом «Крамниця», тост. Розділи
    /// товарів і шторку покупки будує рантайм-скрипт ShopScreenController.
    /// Сцена вантажиться additive поверх MainGarden; sortingOrder 20 — вище
    /// за Профіль/Посадку (10), бо Крамниця відкривається й поверх них
    /// (пілюля кристалів у шапці), але нижче за оверлей тостів (30).
    /// Меню: PlantBreeding → Зібрати екран «Крамниця».
    /// </summary>
    public static class ShopScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/Shop.unity";
        const float W = 390f, H = 844f;

        [MenuItem("PlantBreeding/Зібрати екран «Крамниця»")]
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
            Debug.Log("[PlantBreeding] Екран «Крамниця» зібрано → " + ScenePath);
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
            canvas.sortingOrder = 20;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGO.transform;

            var bg = MakeImage("Background", root, LoadSprite("bg-gradient"), Color.white, Image.Type.Simple);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true; // ловить кліки, щоб не «протикати» у сцену під нею
            var glow = MakeImage("GlowTop", root, LoadSprite("glow"), Rgba(ColGreen, 0.10f), Image.Type.Simple);
            Place(glow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(420, 260));

            var controller = canvasGO.AddComponent<ShopScreenController>();
            controller.fontHead = FontHead;
            controller.fontUi = FontUi;
            controller.sprCard = LoadSprite("rounded-20");
            controller.sprCardLine = LoadSprite("rounded-20-line");
            controller.sprSmall = LoadSprite("rounded-16");
            controller.sprSmallLine = LoadSprite("rounded-16-line");
            controller.sprSheet = LoadSprite("rounded-24");
            controller.sprPill = LoadSprite("pill-16");
            controller.sprCircle = LoadSprite("circle-fill");
            controller.sprCircleLine = LoadSprite("circle-line");
            controller.sprCheck = LoadSprite("icon-check");
            controller.sprGlow = LoadSprite("glow");
            controller.sprGem = LoadSprite("icon-gem", SvgDir);
            controller.sprCoin = LoadSprite("icon-coin", SvgDir);
            controller.sprLock = LoadSprite("icon-lock", SvgDir);
            controller.sprPlus = LoadSprite("icon-plus", SvgDir);
            controller.sprPotCrystal = LoadSprite("shop-pot-crystal", SvgDir);
            controller.sprPotJade = LoadSprite("shop-pot-jade", SvgDir);
            controller.sprPotGold = LoadSprite("shop-pot-gold", SvgDir);
            controller.sprSeedPack = LoadSprite("shop-seed-pack", SvgDir);

            // Єдина шапка всіх екранів (HeaderView); монети/кристали оновлює вона.
            BuildStandardHeader(root, withBack: false, out _);
            BuildTitle(root);
            controller.tabsContent = BuildTabsBar(root);
            controller.listContent = BuildScrollArea(root, 118, -206, out var scroll);
            controller.scrollRect = scroll;

            BuildBottomNav(root);
            BuildToast(root, controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void BuildTitle(Transform root)
        {
            var title = MakeLabel("Title", root, "Крамниця", FontHead, 36, ColText, FontStyles.Normal);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -122), new Vector2(280, 40));
            title.alignment = TextAlignmentOptions.BottomLeft;
        }

        // ── Смуга вкладок (горизонтальний скрол чіпів) ────────────────────
        static Transform BuildTabsBar(Transform root)
        {
            var bar = new GameObject("TabsBar", typeof(RectTransform), typeof(ScrollRect));
            bar.transform.SetParent(root, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0.5f, 1);
            brt.anchoredPosition = new Vector2(0, -166);
            brt.sizeDelta = new Vector2(0, 36);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(bar.transform, false);
            Stretch((RectTransform)viewport.transform);
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.001f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(0, 1);
            crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;
            var hl = content.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 6; hl.padding = new RectOffset(16, 16, 1, 1);
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = bar.GetComponent<ScrollRect>();
            sr.horizontal = true; sr.vertical = false;
            sr.viewport = (RectTransform)viewport.transform;
            sr.content = crt;
            sr.movementType = ScrollRect.MovementType.Clamped;
            return content.transform;
        }

        // ── Скролований вертикальний список ───────────────────────────────
        static Transform BuildScrollArea(Transform parent, float bottomOffset, float topOffset, out ScrollRect scrollRect)
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
            vl.spacing = 12; vl.padding = new RectOffset(16, 16, 6, 24);
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = scrollGO.GetComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true;
            scrollRect.viewport = (RectTransform)viewport.transform;
            scrollRect.content = crt;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return content.transform;
        }

        // ── Тост ──────────────────────────────────────────────────────────
        static void BuildToast(Transform root, ShopScreenController controller)
        {
            var toast = MakeImage("Toast", root, LoadSprite("rounded-12"), new Color(0.08f, 0.09f, 0.06f, 0.95f), Image.Type.Sliced);
            var rt = toast.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -210);
            rt.sizeDelta = new Vector2(320, 44);
            var line = MakeImage("Border", toast.transform, LoadSprite("rounded-12-line"), Rgba(Color.white, 0.14f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            var label = MakeLabel("Label", toast.transform, "", FontUi, 12.5f, Hex("#EBE4D6"), FontStyles.Bold);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(10, 0); label.rectTransform.offsetMax = new Vector2(-10, 0);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;

            toast.transform.SetAsLastSibling();
            toast.gameObject.SetActive(false);
            controller.toastRoot = toast.gameObject;
            controller.toastLabel = label;
        }

        // ── Нижнє меню (активний пункт «Крамниця», «Сад» закриває вкладки) ──
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

            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-garden", "Сад", false, SceneNavButton.ShopScene, SceneNavButton.NavAction.CloseScene);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-lab", "Лабораторія", false, "Lab", SceneNavButton.NavAction.OpenAdditive);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-dex", "Дендрарій", false, "Dex", SceneNavButton.NavAction.OpenAdditive);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-shop", "Крамниця", true, null, SceneNavButton.NavAction.OpenAdditive);
        }
    }
}
