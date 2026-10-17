#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlantBreeding.Core;
using PlantBreeding.Garden;
using PlantBreeding.Save;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Перевірка екранів на різних телефонах (RELEASE-PLAN п. 4) без Device Simulator:
    /// Editor/ScreenAudit запускає Play у batchmode, а цей скрипт по черзі «вдягає»
    /// профілі екранів (16:9 … 21:9, iPad) з вирізом і жестовою смугою, відкриває
    /// кожен екран і знімає його в PNG: Builds/ScreenAudit/&lt;профіль&gt;/&lt;екран&gt;.png.
    ///
    /// Прогрес не зберігається (SaveSystem.Suspended): рослину для картки/догляду
    /// садимо лише в пам'яті. Тільки в редакторі.
    /// </summary>
    public class ScreenAuditRunner : MonoBehaviour
    {
        public const string SessionKey = "PB_ScreenAudit";
        public const string OutDir = "Builds/ScreenAudit";

        private struct Profile
        {
            public string name;
            public int w, h, top, bottom; // пікселі; top/bottom — виріз і жестова смуга
            public Profile(string name, int w, int h, int top, int bottom)
            { this.name = name; this.w = w; this.h = h; this.top = top; this.bottom = bottom; }
        }

        private static readonly Profile[] Profiles =
        {
            new Profile("1_16x9_iPhoneSE", 750, 1334, 40, 0),
            new Profile("2_18x9_Android", 1080, 2160, 80, 0),
            new Profile("3_19.5x9_iPhone14", 1170, 2532, 141, 102),
            new Profile("4_20x9_Android", 1080, 2400, 90, 66),
            new Profile("5_21x9_Xperia", 1080, 2520, 110, 0),
            new Profile("6_4x3_iPad", 1536, 2048, 48, 40),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
            SaveSystem.Suspended = true;
            var go = new GameObject("ScreenAuditRunner");
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenAuditRunner>();
        }

        private Camera _cam;
        private RenderTexture _rt;
        private Profile _p;

        private IEnumerator Start()
        {
            for (int i = 0; i < 10; i++) yield return null; // сад, оверлей і сейв піднялись
            var gm = GameManager.Instance;
            var garden = GardenManager.Current;
            if (gm == null || garden == null) { Finish(1, "немає GameManager/GardenManager"); yield break; }

            // Таймери/тости не повинні заважати знімкам.
            var growing = FindOrPlantGrowing(garden);
            var empty = FindEmpty(garden);

            // Старі знімки — геть, щоб пропущений екран не лишився застарілим.
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);

            var camGo = new GameObject("AuditCamera");
            DontDestroyOnLoad(camGo);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;
            _cam.depth = 100;

            foreach (var p in Profiles)
            {
                _p = p;
                SafeAreaFitter.SimulatedScreen = new Vector2Int(p.w, p.h);
                SafeAreaFitter.SimulatedSafeArea = new Rect(0, p.bottom, p.w, p.h - p.top - p.bottom);
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(p.w, p.h, 24, RenderTextureFormat.ARGB32);
                _cam.targetTexture = _rt;
                Directory.CreateDirectory(Path.Combine(OutDir, p.name));

                yield return Shot("01_garden");

                // Сповіщення вгорі: картка завдання + рядок продажу (макет Сповіщення/export-notify).
                Core.GameEvents.RaiseTaskCompleted(0);
                Core.GameEvents.RaiseSale("violet", "Фіалка продана", 12);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("01b_notifications");
                yield return new WaitForSecondsRealtime(7.5f); // зникли — не заважають наступним знімкам

                var overlay = EconomyOverlayView.Instance;
                if (overlay != null && growing != null)
                {
                    overlay.OpenPlantCard(growing);
                    yield return Shot("02_plant_card");
                    overlay.ClosePlantCard();
                }
                if (overlay != null)
                {
                    overlay.OpenTasks();
                    yield return Shot("03_daily_tasks");
                    overlay.ShowWeekTab();
                    yield return Shot("03b_weekly_tasks");
                    overlay.CloseModal();
                    overlay.OpenSettings();
                    yield return Shot("04_settings");
                    overlay.CloseSettings();
                }

                // Посадку знімаємо завжди: без вільної грядки — з будь-якою (екран лише рендериться).
                var plantTarget = empty ?? (garden.Plots.Count > 0 ? garden.Plots[0] : null);
                if (plantTarget != null)
                {
                    PlantingRequest.Set(plantTarget, garden);
                    yield return SceneShot("Planting", "05_planting");
                    PlantingRequest.Clear();
                }
                if (growing != null)
                {
                    CareRequest.Set(growing, garden);
                    yield return SceneShot(GardenManager.CareSceneName, "06_care");
                    CareRequest.Clear();
                }
                yield return SceneShot("Lab", "07_lab");
                yield return SceneShot("Dex", "08_dex", () =>
                {
                    var dex = FindAnyObjectByType<DexScreenController>();
                    if (dex != null) dex.OpenCollection("windowsill"); // фіалка — перший відкритий вид
                    return "08b_dex_detail";
                });
                yield return SceneShot("Shop", "09_shop");
                // Усі аватари й рамки «відкриті» (лише в пам'яті — SaveSystem.Suspended), щоб видно було всі малюнки.
                var pd = gm.playerData;
                foreach (var av in Collections.ProfileItemCatalog.Avatars) if (!pd.ownedAvatarIds.Contains(av.id)) pd.ownedAvatarIds.Add(av.id);
                foreach (var fr in Collections.ProfileItemCatalog.Frames) if (!pd.ownedFrameIds.Contains(fr.id)) pd.ownedFrameIds.Add(fr.id);
                pd.activeAvatarId = "bee"; pd.activeFrameId = "floral";
                yield return SceneShot("Profile", "10_profile", () =>
                {
                    var scroll = FindAnyObjectByType<ProfileScreenController>()?.GetComponentInChildren<ScrollRect>();
                    if (scroll != null) scroll.verticalNormalizedPosition = 0f; // донизу — сітка рамок
                    return "10b_profile_frames";
                });
            }

            Finish(0, "готово");
        }

        private static PlotSlot FindOrPlantGrowing(GardenManager garden)
        {
            foreach (var s in garden.Plots)
                if (s.plant != null && (s.state == PlotState.Growing || s.state == PlotState.NeedsWater)) return s;
            var empty = FindEmpty(garden);
            if (empty == null || PlantCatalog.All.Count == 0) return null;
            PlantData pick = PlantCatalog.All[0];
            foreach (var p in PlantCatalog.All) if (p.growTimeSeconds > 3600) { pick = p; break; } // таймер на години
            empty.Plant(pick, null, StarterBoostKind.None);
            return empty;
        }

        private static PlotSlot FindEmpty(GardenManager garden)
        {
            foreach (var s in garden.Plots) if (s.state == PlotState.Empty) return s;
            return null;
        }

        /// <param name="then">Дія після першого знімка (відкрити вікно) → ім'я другого знімка.</param>
        private IEnumerator SceneShot(string scene, string file, System.Func<string> then = null)
        {
            SceneManager.LoadScene(scene, LoadSceneMode.Additive);
            yield return null;
            yield return Shot(file);
            if (then != null) yield return Shot(then());
            var loaded = SceneManager.GetSceneByName(scene);
            if (loaded.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(loaded);
                while (op != null && !op.isDone) yield return null;
            }
        }

        /// <summary>Перевести всі Canvas на камеру з RenderTexture профілю і зняти кадр.</summary>
        private IEnumerator Shot(string file)
        {
            // Масштаб як у CanvasScaler (390×844, match 0.5) — але від розміру профілю, не вікна.
            float scale = Mathf.Sqrt(_p.w / 390f * (_p.h / 844f));
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _cam;
                canvas.planeDistance = 10f;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = scale;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
            if (!file.Contains("notifications")) HideToasts();

            var req = new RenderPipeline.StandardRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
            else _cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_p.w, _p.h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _p.w, _p.h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            DrawSafeAreaLines(tex);
            File.WriteAllBytes(Path.Combine(OutDir, _p.name, file + ".png"), tex.EncodeToPNG());
            Destroy(tex);

            var root = FindAnyObjectByType<Canvas>();
            Debug.Log($"[ScreenAudit] {_p.name}/{file} canvas={(root != null ? ((RectTransform)root.transform).rect.size.ToString() : "-")}");
        }

        /// <summary>Червоні лінії меж safe area — видно, що заходить під виріз/смугу.</summary>
        private void DrawSafeAreaLines(Texture2D tex)
        {
            var red = new Color(1f, 0.15f, 0.15f);
            for (int x = 0; x < _p.w; x += 2)
            {
                if (_p.top > 0) tex.SetPixel(x, _p.h - _p.top, red);
                if (_p.bottom > 0) tex.SetPixel(x, _p.bottom, red);
            }
            tex.Apply();
        }

        private static void HideToasts()
        {
            foreach (var cg in FindObjectsByType<CanvasGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (cg.gameObject.name == "RowNotice" || cg.gameObject.name == "TaskNotice") cg.alpha = 0f;
        }

        private void Finish(int code, string message)
        {
            Debug.Log($"[ScreenAudit] Кінець: {message}");
            UnityEditor.SessionState.SetBool(SessionKey, false);
            SafeAreaFitter.SimulatedScreen = null;
            SafeAreaFitter.SimulatedSafeArea = null;
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
