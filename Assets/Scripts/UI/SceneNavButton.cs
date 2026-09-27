using System.Collections;
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

        public NavAction action = NavAction.OpenAdditive;
        public string sceneName;

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (action == NavAction.OpenAdditive)
            {
                if (SceneManager.GetSceneByName(sceneName).isLoaded) return;
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            }
            else
            {
                StartCoroutine(UnloadNextFrame());
            }
        }

        /// <summary>
        /// Вивантаження саме на цій кнопці знищує GameObject кнопки разом з
        /// усією сценою — а InputSystemUIInputModule дообробляє поточний
        /// pointer-цикл (ProcessPointerMovement) уже ПІСЛЯ onClick, тримаючи
        /// raw-посилання на об'єкт для hover-трекінгу. Синхронний
        /// UnloadSceneAsync тут кидає MissingReferenceException щоразу при
        /// перемиканні вкладок. Відкладання на 1 кадр дає InputModule
        /// завершити поточний цикл до знищення.
        /// </summary>
        private IEnumerator UnloadNextFrame()
        {
            yield return null;
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }
}
