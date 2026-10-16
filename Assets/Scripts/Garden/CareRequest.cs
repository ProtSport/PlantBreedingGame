namespace PlantBreeding.Garden
{
    /// <summary>
    /// Міст між карткою рослини (оверлей саду) і додатково завантаженою сценою
    /// «Догляд» — як PlantingRequest для Посадки. Контролер екрану догляду
    /// читає слот у Start(), а свайпом перемикається на сусідні грядки
    /// через SourceManager.Plots.
    /// </summary>
    public static class CareRequest
    {
        public static PlotSlot TargetSlot { get; private set; }
        public static GardenManager SourceManager { get; private set; }

        public static void Set(PlotSlot slot, GardenManager manager)
        {
            TargetSlot = slot;
            SourceManager = manager;
        }

        public static void Clear()
        {
            TargetSlot = null;
            SourceManager = null;
        }
    }
}
