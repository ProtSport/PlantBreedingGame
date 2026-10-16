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
    /// Збирає каркас сцени «Догляд» (RELEASE-PLAN п. 3): фон, уніфікована шапка
    /// з «назад», заголовок і область Content між шапкою й низом екрана.
    /// Сцену з рослиною, дії «Полив»/«Світло» і свайп будує рантайм-скрипт
    /// CareScreenController. Вантажиться additive поверх MainGarden з картки
    /// рослини; sortingOrder 10 — як Посадка (оверлей тостів 30 — вище).
    /// Меню: PlantBreeding → Зібрати екран «Догляд».
    /// </summary>
    public static class CareScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/Care.unity";
        const float W = 390f, H = 844f;

        [MenuItem("PlantBreeding/Зібрати екран «Догляд»")]
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
            Debug.Log("[PlantBreeding] Екран «Догляд» зібрано → " + ScenePath);
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
            bg.raycastTarget = true; // ловить кліки, щоб не «протикати» в сад під нею
            var glow = MakeImage("GlowTop", root, LoadSprite("glow"), Rgba(ColGreen, 0.12f), Image.Type.Simple);
            Place(glow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(420, 300));

            var controller = canvasGO.AddComponent<CareScreenController>();
            controller.fontHead = FontHead;
            controller.fontUi = FontUi;
            controller.sprCard = LoadSprite("rounded-20");
            controller.sprCardLine = LoadSprite("rounded-20-line");
            controller.sprPill = LoadSprite("pill-16");
            controller.sprCircle = LoadSprite("circle-fill");
            controller.sprGlow = LoadSprite("glow");
            controller.sprWater = LoadSprite("care-water", SvgDir);
            controller.sprSun = LoadSprite("care-sun", SvgDir);

            BuildStandardHeader(root, withBack: true, out var backButton);
            controller.backButton = backButton;

            var title = MakeLabel("Title", root, "Догляд", FontHead, 36, ColText, FontStyles.Normal);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -122), new Vector2(280, 40));
            title.alignment = TextAlignmentOptions.BottomLeft;

            // Розтягнута область — SafeAreaFitter стискає її з обох боків.
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(0, 20);
            crt.offsetMax = new Vector2(0, -176);
            controller.content = crt;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
