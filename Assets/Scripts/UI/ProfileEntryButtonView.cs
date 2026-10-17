using UnityEngine;
using UnityEngine.UI;
using PlantBreeding.Collections;
using PlantBreeding.Core;

namespace PlantBreeding.UI
{
    /// <summary>
    /// Кругла кнопка-аватар у шапці (точка входу в «Профіль»): обраний аватар у
    /// обраній рамці (ProfileArt). Оновлюється при поверненні з Профілю.
    /// </summary>
    public class ProfileEntryButtonView : MonoBehaviour
    {
        public Image ringImage;    // рамка (поверх)
        public Image avatarImage;  // аватар (під рамкою)

        private string _shownAvatar, _shownFrame;

        private void Start() => Redraw();

        private void Update()
        {
            // Аватар/рамку міняють у Профілі (інша сцена) — дешева перевірка без подій.
            var data = GameManager.Instance != null ? GameManager.Instance.playerData : null;
            if (data != null && (data.activeAvatarId != _shownAvatar || data.activeFrameId != _shownFrame)) Redraw();
        }

        private void Redraw()
        {
            var data = GameManager.Instance != null ? GameManager.Instance.playerData : null;
            if (data == null) return;
            _shownAvatar = data.activeAvatarId;
            _shownFrame = data.activeFrameId;
            if (ringImage != null) { ringImage.sprite = ProfileArt.Frame(_shownFrame); ringImage.color = Color.white; ringImage.preserveAspect = true; }
            if (avatarImage != null) { avatarImage.sprite = ProfileArt.Avatar(_shownAvatar); avatarImage.color = Color.white; }
        }
    }
}
