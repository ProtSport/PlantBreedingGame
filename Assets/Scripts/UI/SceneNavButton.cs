using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Кнопка нижнього меню, що вантажить/вивантажує іншу сцену additive
    /// (Сад ⇄ Лабораторія тощо). Слухач onClick МАЄ підключатись у Start()
    /// рантайм-скрипта, а не в Editor-білдері сцени — AddListener, викликаний
    /// у Editor-коді під час побудови, ніколи не серіалізується в сцену і
    /// зникає при вході в Play-режим (не плутати з `Button.onPersistentClick`,
    /// заданим через інспектор — той серіалізується, runtime AddListener ні).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SceneNavButton : MonoBehaviour
    {
        public enum NavAction { OpenAdditive, CloseScene }

        public const string ShopScene = "Shop";

        public NavAction action = NavAction.OpenAdditive;
        public string sceneName;

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(HandleClick);
        }

        /// <summary>
        /// Сцени-вкладки нижнього меню, що відкриваються additive поверх
        /// MainGarden. Одночасно може бути відкрита лише ОДНА: раніше
        /// Лабораторія й Дендрарій накладались одна на одну (обидві Canvas
        /// sortingOrder 10, порядок малювання невизначений) — видно було не
        /// ту панель меню з не тим підсвіченим пунктом, «Сад» з Дендрарію
        /// повертав у Лабораторію, а «Лабораторія» з Дендрарію не робила
        /// нічого (сцена вже була завантажена під низом).
        /// </summary>
        private static readonly string[] TabScenes = { "Lab", "Dex", ShopScene };

        private static bool IsTab(string scene) => System.Array.IndexOf(TabScenes, scene) >= 0;

        private void HandleClick()
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (action == NavAction.OpenAdditive)
            {
                if (SceneManager.GetSceneByName(sceneName).isLoaded) return;
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                if (IsTab(sceneName)) StartCoroutine(UnloadNextFrame(OtherLoadedTabs(sceneName)));
            }
            else
            {
                // «Сад» із вкладки закриває всі вкладки; решта (Профіль, Посадка) — лише себе.
                var toClose = IsTab(sceneName) ? OtherLoadedTabs(null) : new List<string> { sceneName };
                StartCoroutine(UnloadNextFrame(toClose));
            }
        }

        /// <summary>
        /// Відкрити вкладку не з кнопки меню (напр. Крамницю з пілюлі кристалів
        /// чи вікна прискорення): як OpenAdditive, але корутина вивантаження
        /// інших вкладок живе на GameManager (DontDestroyOnLoad), бо кнопка, що
        /// викликала перехід, може бути в сцені, яку зараз вивантажимо.
        /// </summary>
        public static void OpenTab(string scene)
        {
            if (SceneManager.GetSceneByName(scene).isLoaded) return;
            SceneManager.LoadScene(scene, LoadSceneMode.Additive);
            var runner = Core.GameManager.Instance;
            if (runner != null && IsTab(scene)) runner.StartCoroutine(UnloadScenesNextFrame(OtherLoadedTabs(scene)));
        }

        private static IEnumerator UnloadScenesNextFrame(List<string> scenes)
        {
            yield return null;
            foreach (var scene in scenes)
                if (SceneManager.GetSceneByName(scene).isLoaded) SceneManager.UnloadSceneAsync(scene);
        }

        private static List<string> OtherLoadedTabs(string keep)
        {
            var list = new List<string>();
            foreach (var tab in TabScenes)
                if (tab != keep && SceneManager.GetSceneByName(tab).isLoaded) list.Add(tab);
            return list;
        }

        /// <summary>
        /// Вивантаження саме на цій кнопці знищує GameObject кнопки разом з
        /// усією сценою — а InputSystemUIInputModule дообробляє поточний
        /// pointer-цикл (ProcessPointerMovement) уже ПІСЛЯ onClick, тримаючи
        /// raw-посилання на об'єкт для hover-трекінгу. Синхронний
        /// UnloadSceneAsync тут кидає MissingReferenceException щоразу при
        /// перемиканні вкладок. Відкладання на 1 кадр дає InputModule
        /// завершити поточний цикл до знищення. Власна сцена — останньою,
        /// бо разом з нею зупиняється й ця корутина.
        /// </summary>
        private IEnumerator UnloadNextFrame(List<string> scenes)
        {
            yield return null;
            string own = gameObject.scene.name;
            foreach (var scene in scenes)
                if (scene != own) SceneManager.UnloadSceneAsync(scene);
            if (scenes.Contains(own)) SceneManager.UnloadSceneAsync(own);
        }
    }
}
