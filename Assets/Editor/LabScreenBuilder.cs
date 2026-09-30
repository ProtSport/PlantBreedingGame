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
    /// Збирає статичний каркас сцени "Лабораторія" за дизайном з папки
    /// "Лабараторія/export/lab-research.html": шапка (той самий kicker+title
    /// патерн, що Мій сад/Посадка — offset-и НЕ вигадувати заново), рівень
    /// лабораторії, скролований список гілок досліджень, нижнє меню.
    /// Наповнення карток гілок — рантайм-скрипт LabScreenController.
    /// Сцена завантажується завжди additive поверх MainGarden (без власних
    /// Camera/Light/EventSystem), так само як "Посадка".
    /// Меню: PlantBreeding → Зібрати екран «Лабораторія».
    /// </summary>
    public static class LabScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/Lab.unity";
        const float W = 390f, H = 844f;

        [MenuItem("PlantBreeding/Зібрати екран «Лабораторія»")]
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
            Debug.Log("[PlantBreeding] Екран «Лабораторія» зібрано → " + ScenePath);
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
            canvas.sortingOrder = 10; // поверх Canvas сцени саду, як "Посадка"
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGO.transform;

            var bg = MakeImage("Background", root, LoadSprite("bg-gradient"), Color.white, Image.Type.Simple);
            Stretch(bg.rectTransform);
            var glow = MakeImage("GlowTopRight", root, LoadSprite("glow"), Rgba(ColBlue, 0.14f), Image.Type.Simple);
            Place(glow.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-70, -50), new Vector2(220, 220));

            var controller = canvasGO.AddComponent<LabScreenController>();
            controller.fontHead = FontHead;
            controller.fontUi = FontUi;
            controller.sprRoundedCard = LoadSprite("rounded-20");
            controller.sprRoundedCardLine = LoadSprite("rounded-20-line");
            controller.sprRoundedSmall = LoadSprite("rounded-12");
            controller.sprRoundedSmallLine = LoadSprite("rounded-12-line");
            controller.sprPillTiny = LoadSprite("pill-tiny");
            controller.sprCircleFill = LoadSprite("circle-fill");
            controller.sprCircleLine = LoadSprite("circle-line");
            controller.sprGlow = LoadSprite("glow");
            controller.sprIconBolt = LoadSprite("icon-bolt");
            controller.sprIconHeart = LoadSprite("icon-heart");
            controller.sprIconStar = LoadSprite("icon-star");
            controller.sprIconCoin = LoadSprite("icon-coin", SvgDir);
            controller.sprIconCheck = LoadSprite("icon-check");
            controller.sprIconLock = LoadSprite("icon-lock", SvgDir);

            // Єдина шапка всіх екранів (HeaderView); монети/кристали оновлює вона.
            BuildStandardHeader(root, withBack: false, out _);
            BuildTitle(root);

            controller.branchesContent = BuildScrollArea(root);
            BuildToast(root, controller);
            BuildBottomNav(root);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ── Заголовок під шапкою (позиція як «Мій сад») ──────────────────
        static void BuildTitle(Transform root)
        {
            var title = MakeLabel("Title", root, "Дослідження", FontHead, 36, ColText, FontStyles.Normal);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -122), new Vector2(280, 40));
            title.alignment = TextAlignmentOptions.BottomLeft;
        }

        // ── Скролований вертикальний список карток гілок ──────────────────
        static Transform BuildScrollArea(Transform root)
        {
            var scrollGO = new GameObject("ScrollArea", typeof(RectTransform), typeof(ScrollRect));
            scrollGO.transform.SetParent(root, false);
            var srt = (RectTransform)scrollGO.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(0, 118);  // над нижнім меню, як Grid на "Мій сад"
            srt.offsetMax = new Vector2(0, -180); // під шапкою, як "Посадка"

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
            vl.spacing = 14; vl.padding = new RectOffset(20, 20, 4, 24);
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

        // ── Тост-повідомлення (напр. "Спочатку заверши поточне дослідження") ─
        static void BuildToast(Transform root, LabScreenController controller)
        {
            var toast = MakeImage("Toast", root, LoadSprite("rounded-12"), new Color(0.08f, 0.09f, 0.06f, 0.95f), Image.Type.Sliced);
            var rt = toast.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -174);
            rt.sizeDelta = new Vector2(320, 40);
            var line = MakeImage("Border", toast.transform, LoadSprite("rounded-12-line"), Rgba(Color.white, 0.14f), Image.Type.Sliced);
            Stretch(line.rectTransform);

            var label = MakeLabel("Label", toast.transform, "", FontUi, 12.5f, Hex("#EBE4D6"), FontStyles.Bold);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;

            toast.transform.SetAsLastSibling(); // над усім, у т.ч. над нижнім меню
            toast.gameObject.SetActive(false);

            controller.toastRoot = toast.gameObject;
            controller.toastLabel = label;
        }

        // ── Нижнє меню: та сама скляна панель, що на "Мій сад", тепер з
        //    активним пунктом "Лабораторія" і клікабельним "Сад" ────────────
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

            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-garden", "Сад", false, "Lab", SceneNavButton.NavAction.CloseScene);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-lab", "Лабораторія", true, null, SceneNavButton.NavAction.OpenAdditive);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-dex", "Дендрарій", false, "Dex", SceneNavButton.NavAction.OpenAdditive);
            HomeScreenBuilder.MakeNavItem(nav.transform, "nav-shop", "Крамниця", false, SceneNavButton.ShopScene, SceneNavButton.NavAction.OpenAdditive);
        }
    }
}
