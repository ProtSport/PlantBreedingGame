namespace PlantBreeding.UI
{
    /// <summary>
    /// Вхід у Крамницю з будь-якого екрану: пілюля кристалів у шапці
    /// (HeaderView) і кнопка «купити» прискорення в картці рослини, коли бракує
    /// кристалів. Сама Крамниця — окрема сцена «Shop» (ShopScreenController),
    /// вкладка нижнього меню, як Лабораторія й Дендрарій.
    /// </summary>
    public partial class EconomyOverlayView
    {
        public void OpenShop()
        {
            // Вікна оверлею малюються над усіма сценами — закриваємо, щоб не перекрили Крамницю.
            if (_cardModal != null) _cardModal.SetActive(false);
            if (_treatModal != null) _treatModal.SetActive(false);
            SceneNavButton.OpenTab(SceneNavButton.ShopScene);
        }

        /// <summary>Баланс змінився (напр. після покупки) — прискорення в картці рослини могло стати доступним.</summary>
        private void RefreshShop() => RefreshPlantCard();
    }
}
