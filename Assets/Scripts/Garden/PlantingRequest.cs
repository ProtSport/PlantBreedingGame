namespace PlantBreeding.Garden
{
    /// <summary>
    /// Міст між сценою "Мій сад" і додатково завантаженою сценою "Посадка".
    /// GardenManager записує сюди слот перед завантаженням сцени; контролер
    /// екрану Посадки читає його в Start() і застосовує вибір напряму до
    /// того самого об'єкта PlotSlot (без пересилання через SaveSystem).
    /// </summary>
    public static class PlantingRequest
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
