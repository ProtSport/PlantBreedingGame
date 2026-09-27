namespace PlantBreeding.Lab
{
    /// <summary>Незалежні гілки апгрейдів Лабораторії (розділ 5.3 GDD, спрощена версія без черги).</summary>
    public enum LabBranchId
    {
        Speed,
        Health,
        Yield
    }

    /// <summary>
    /// Один вузол (рівень) гілки. <see cref="magnitude"/> — АБСОЛЮТНЕ значення ефекту
    /// на цьому рівні (не приріст!): напр. Швидкість idx0..3 = -5%/-10%/-15%/-20% часу
    /// росту — завершення idx1 означає "-10% загалом", а не "-5% ще додатково до idx0".
    /// Сенс magnitude для гілки Врожайність різний по вузлах (перші два — бонус ціни,
    /// останні два — шанс подвійного врожаю) — див. LabResearchService.
    /// </summary>
    public readonly struct LabNodeDef
    {
        public readonly string effectLine1;
        public readonly string effectLine2;
        public readonly int coinCost;
        public readonly int gemCost;
        public readonly float durationSeconds;
        public readonly float magnitude;
        /// <summary>Максимальна ціна миттєвого прискорення (на самому початку таймера); дешевшає лінійно до кінця.</summary>
        public readonly int maxRushGemCost;
        /// <summary>Кристали-нагорода за завершення цього вузла (розділ 10.4 GDD, "завершене дослідження").</summary>
        public readonly int gemReward;

        public LabNodeDef(string effectLine1, string effectLine2, int coinCost, int gemCost,
            float durationSeconds, float magnitude, int maxRushGemCost, int gemReward)
        {
            this.effectLine1 = effectLine1;
            this.effectLine2 = effectLine2;
            this.coinCost = coinCost;
            this.gemCost = gemCost;
            this.durationSeconds = durationSeconds;
            this.magnitude = magnitude;
            this.maxRushGemCost = maxRushGemCost;
            this.gemReward = gemReward;
        }
    }

    public class LabBranchDef
    {
        public LabBranchId id;
        public string displayName;
        public string subLabel;
        /// <summary>"green" | "blue" | "gold" — узгоджено з ACCENT у HTML-макеті lab-research.html.</summary>
        public string accent;
        public LabNodeDef[] nodes; // рівно 4, проходяться строго по порядку
    }

    /// <summary>
    /// Статичні дані 3 гілок × 4 вузли. Ціни/тривалості — орієнтовні числа
    /// користувача (300-500 / 700-900 / 1200-1500 / 2000+ монет, капстоун і
    /// кристалами), однакові по індексу вузла в усіх гілках. Тривалості —
    /// реальний ігровий баланс (не демо-скорочення на кшталт ContentSeeder),
    /// але для швидкого тестування є "Прискорити" за кристали.
    /// </summary>
    public static class LabResearchCatalog
    {
        // Індекс:        0            1             2              3 (капстоун)
        static readonly int[] CoinCost = { 400, 800, 1400, 2200 };
        static readonly int[] GemCost = { 0, 0, 0, 10 };
        static readonly float[] DurationSeconds = { 20 * 60, 90 * 60, 4 * 3600, 10 * 3600 };
        static readonly int[] MaxRushGemCost = { 8, 14, 22, 30 };
        // Кристали за дослідження не нараховуються: після стартового подарунка
        // кристали лише купуються в Крамниці (docs/ECONOMY.md).
        static readonly int[] GemReward = { 0, 0, 0, 0 };

        public static readonly LabBranchDef[] Branches =
        {
            new LabBranchDef
            {
                id = LabBranchId.Speed,
                displayName = "Швидкість росту",
                subLabel = "менше часу до врожаю",
                accent = "green",
                nodes = new[]
                {
                    Node(0, "−5%", "час", -0.05f),
                    Node(1, "−10%", "час", -0.10f),
                    Node(2, "−15%", "час", -0.15f),
                    Node(3, "−20%", "час", -0.20f),
                }
            },
            new LabBranchDef
            {
                id = LabBranchId.Health,
                displayName = "Здоров'я",
                subLabel = "менше хвороб і шкідників",
                accent = "blue",
                nodes = new[]
                {
                    Node(0, "−10%", "хвороба", 0.10f),
                    Node(1, "−20%", "хвороба", 0.20f),
                    Node(2, "швидше", "віднов.", 0f),
                    Node(3, "імунітет", "до шкідн.", 1f),
                }
            },
            new LabBranchDef
            {
                id = LabBranchId.Yield,
                displayName = "Врожайність",
                subLabel = "більше прибутку з рослини",
                accent = "gold",
                nodes = new[]
                {
                    Node(0, "+5%", "ціна", 0.05f),
                    Node(1, "+10%", "ціна", 0.10f),
                    Node(2, "+5% 2×", "врожай", 0.05f),
                    Node(3, "+10% 2×", "врожай", 0.10f),
                }
            },
        };

        static LabNodeDef Node(int index, string line1, string line2, float magnitude) => new LabNodeDef(
            line1, line2, CoinCost[index], GemCost[index], DurationSeconds[index], magnitude,
            MaxRushGemCost[index], GemReward[index]);

        public static LabBranchDef GetBranch(LabBranchId id)
        {
            foreach (var b in Branches)
                if (b.id == id) return b;
            return Branches[0];
        }

        public static LabNodeDef GetNode(LabBranchId id, int index) => GetBranch(id).nodes[index];
    }
}
