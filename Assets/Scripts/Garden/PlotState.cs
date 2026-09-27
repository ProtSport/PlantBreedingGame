namespace PlantBreeding.Garden
{
    /// <summary>Стани однієї грядки на екрані саду.</summary>
    public enum PlotState
    {
        Locked,     // 🔒 не розблокована (потрібен рівень/оплата)
        Empty,      // + порожня, можна посадити
        Growing,    // ⏳ рослина росте, таймер іде
        NeedsWater, // 💧 потребує поливу
        Sick,       // 🐛 хвороба/шкідник, потрібне лікування
        Ready       // ✅ готова до збору
    }
}
