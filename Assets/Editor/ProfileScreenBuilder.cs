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
    /// Збирає статичний каркас сцени «Профіль» за дизайном з
    /// Персонаж/export/profile-screen.html: шапка (назад · «Профіль» ·
    /// налаштування, БЕЗ нижнього меню — це не вкладка таб-бару, а
    /// під-екран, що відкривається з іконки аватара на «Моєму саду»),
    /// скрол-контент (наповнюється рантайм-контролером), оверлей
    /// перейменування, тост. Сцена завантажується additive поверх
    /// MainGarden, як «Посадка»/«Лабораторія»/«Дендрарій».
    /// Меню: PlantBreeding → Зібрати екран «Профіль».
    /// </summary>
    public static class ProfileScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/Profile.unity";
        const float W = 390f, H = 844f;

        [MenuItem("PlantBreeding/Зібрати екран «Профіль»")]
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
            Debug.Log("[PlantBreeding] Екран «Профіль» зібрано → " + ScenePath);
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
            bg.raycastTarget = true;
            var glow = MakeImage("Glow", root, LoadSprite("glow"), Rgba(ColGoldLt, 0.16f), Image.Type.Simple);
            Place(glow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(300, 200));

            var controller = canvasGO.AddComponent<ProfileScreenController>();
            controller.fontHead = FontHead;
            controller.fontUi = FontUi;
            controller.sprRoundedCard = LoadSprite("rounded-20");
            controller.sprRoundedCardLine = LoadSprite("rounded-20-line");
            controller.sprRoundedSmall = LoadSprite("rounded-16");
            controller.sprRoundedSmallLine = LoadSprite("rounded-16-line");
            controller.sprPillTiny = LoadSprite("pill-10");
            controller.sprSprig = LoadSprite("sprig");
            controller.sprIconCheck = LoadSprite("icon-check");
            controller.sprCircleFill = LoadSprite("circle-fill");
            controller.sprCircleLine = LoadSprite("circle-line");
            controller.sprGlow = LoadSprite("glow");

            // Єдина шапка всіх екранів (HeaderView) + «назад»; контент — під рядком рівня.
            BuildStandardHeader(root, withBack: true, out var backButton);
            controller.backButton = backButton;
            controller.contentRoot = BuildScrollArea(root, 30, -128);
            BuildRenamePanel(root, controller);
            BuildToast(root, controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ── Скролований вертикальний список (той самий патерн, що Dex) ─────
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
            vl.spacing = 13; vl.padding = new RectOffset(18, 18, 6, 30);
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            vl.childAlignment = TextAnchor.UpperCenter;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGO.GetComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.viewport = (RectTransform)viewport.transform;
            sr.content = crt;
            sr.movementType = ScrollRect.MovementType.Clamped;

            return content.transform;
        }

        // ── Оверлей перейменування ──────────────────────────────────────────
        static void BuildRenamePanel(Transform root, ProfileScreenController controller)
        {
            var panel = new GameObject("RenamePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root, false);
            Stretch((RectTransform)panel.transform);
            var backdrop = panel.GetComponent<Image>();
            backdrop.color = new Color(0, 0, 0, 0.6f);
            backdrop.raycastTarget = true;

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(panel.transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(300, 190);
            var cardBg = card.GetComponent<Image>();
            cardBg.sprite = LoadSprite("rounded-20"); cardBg.type = Image.Type.Sliced;
            cardBg.color = Hex("#1A1E11");
            cardBg.raycastTarget = true;
            var cvl = card.GetComponent<VerticalLayoutGroup>();
            cvl.padding = new RectOffset(20, 20, 20, 20); cvl.spacing = 14;
            cvl.childControlWidth = true; cvl.childControlHeight = true;
            cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;

            var title = MakeLabel("Title", card.transform, "Нове ім'я", FontHead, 22, ColText, FontStyles.Normal);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            inputGo.transform.SetParent(card.transform, false);
            inputGo.GetComponent<LayoutElement>().preferredHeight = 42;
            var inputBg = inputGo.GetComponent<Image>();
            inputBg.sprite = LoadSprite("rounded-12"); inputBg.type = Image.Type.Sliced;
            inputBg.color = Rgba(Color.white, 0.06f);
            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGo.transform, false);
            var tart = (RectTransform)textArea.transform;
            tart.anchorMin = Vector2.zero; tart.anchorMax = Vector2.one;
            tart.offsetMin = new Vector2(12, 4); tart.offsetMax = new Vector2(-12, -4);
            var placeholder = MakeLabel("Placeholder", textArea.transform, "Ім'я...", FontUi, 15, Rgba(Color.white, 0.35f), FontStyles.Normal);
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            var textLabel = MakeLabel("Text", textArea.transform, "", FontUi, 15, ColText, FontStyles.Normal);
            Stretch(textLabel.rectTransform);
            textLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var inputField = inputGo.GetComponent<TMP_InputField>();
            inputField.textViewport = tart;
            inputField.textComponent = textLabel;
            inputField.placeholder = placeholder;
            inputField.characterLimit = 18;
            controller.renameInput = inputField;

            var btnRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            btnRow.transform.SetParent(card.transform, false);
            btnRow.GetComponent<LayoutElement>().preferredHeight = 42;
            var brhl = btnRow.GetComponent<HorizontalLayoutGroup>();
            brhl.spacing = 10; brhl.childControlWidth = true; brhl.childControlHeight = true;
            brhl.childForceExpandWidth = true; brhl.childForceExpandHeight = true;

            controller.renameCancelButton = MakeDialogButton(btnRow.transform, "Скасувати", Rgba(Color.white, 0.06f), Hex("#C9CFC1"));
            controller.renameConfirmButton = MakeDialogButton(btnRow.transform, "Зберегти", Hex("#8FBF5A"), Hex("#0E130C"));

            controller.renamePanel = panel;
        }

        static Button MakeDialogButton(Transform parent, string label, Color bg, Color textColor)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = LoadSprite("rounded-12"); img.type = Image.Type.Sliced; img.color = bg;
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var lbl = MakeLabel("Label", go.transform, label, FontUi, 13, textColor, FontStyles.Bold);
            Stretch(lbl.rectTransform);
            lbl.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        // ── Тост ──────────────────────────────────────────────────────────
        static void BuildToast(Transform root, ProfileScreenController controller)
        {
            var toast = MakeImage("Toast", root, LoadSprite("rounded-12"), new Color(0.08f, 0.09f, 0.06f, 0.95f), Image.Type.Sliced);
            var rt = toast.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -104);
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
    }
}
