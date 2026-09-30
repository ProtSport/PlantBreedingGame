namespace PlantBreeding.UI
{
    /// <summary>
    /// Вхід у Крамницю з будь-якого екрану: пілюля кристалів у шапці
    /// (HeaderView) і кнопка «купити» у вікні прискорення, коли бракує
    /// кристалів. Сама Крамниця — окрема сцена «Shop» (ShopScreenController),
    /// вкладка нижнього меню, як Лабораторія й Дендрарій.
    /// </summary>
    public partial class EconomyOverlayView
    {
        public void OpenShop()
        {
            // Вікна оверлею малюються над усіма сценами — закриваємо, щоб не перекрили Крамницю.
            if (_speedModal != null) _speedModal.SetActive(false);
            if (_treatModal != null) _treatModal.SetActive(false);
            SceneNavButton.OpenTab(SceneNavButton.ShopScene);
        }

        /// <summary>Баланс змінився (напр. після покупки) — вікно прискорення могло стати доступним.</summary>
        private void RefreshShop() => RefreshSpeedUp();
    }
}
