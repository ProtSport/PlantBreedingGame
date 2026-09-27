namespace PlantBreeding.Garden
{
    /// <summary>
    /// Одноразовий бустер, застосований у момент посадки (StarterBoost).
    /// НЕ плутати з добривом на екрані догляду (яке лікує в'янення під час росту) —
    /// в UI обидва звуться "добриво", але це різні дії в коді.
    /// </summary>
    public enum StarterBoostKind
    {
        None,
        Growth
        // Mutation (2) прибрано: мутацій/схрещування на цьому етапі немає.
    }

    public readonly struct StarterBoostDef
    {
        public readonly StarterBoostKind kind;
        public readonly string displayName;
        public readonly string subLabel;
        public readonly float growTimeModifier;
        /// <summary>Ціна в монетах, списується лише при підтвердженій посадці (не при виборі картки).</summary>
        public readonly int coinCost;

        public StarterBoostDef(StarterBoostKind kind, string displayName, string subLabel,
            float growTimeModifier, int coinCost)
        {
            this.kind = kind;
            this.displayName = displayName;
            this.subLabel = subLabel;
            this.growTimeModifier = growTimeModifier;
            this.coinCost = coinCost;
        }
    }

    public static class StarterBoostCatalog
    {
        public static readonly StarterBoostDef[] All =
        {
            new StarterBoostDef(StarterBoostKind.None, "Без добрива", "за замовчуванням", 0f, 0),
            new StarterBoostDef(StarterBoostKind.Growth, "Ріст-буст", "-30% часу росту", -0.30f, 15),
        };

        public static StarterBoostDef Get(StarterBoostKind kind)
        {
            foreach (var def in All)
            {
                if (def.kind == kind) return def;
            }
            return All[0];
        }
    }
}
