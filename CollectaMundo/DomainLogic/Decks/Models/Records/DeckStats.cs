namespace CollectaMundo.DomainLogic.Decks.Models.Records
{
    public sealed record DeckStats
    {
        public int CardCount { get; init; }
        public int CreatureCount { get; init; }
        public int LandCount { get; init; }
        public int SpellCount { get; init; }
        public int NonLandCardCount { get; init; }


        public double CreaturePercentage { get; init; }
        public double LandPercentage { get; init; }
        public double SpellPercentage { get; init; }

        public IReadOnlyList<DeckStatsBucket> TypeBreakdown { get; init; } = [];
        public IReadOnlyList<DeckStatsBucket> ColorBreakdown { get; init; } = [];
        public IReadOnlyList<ManaCurveBucket> ManaCurve { get; init; } = [];
        public int ManaCurveMaxCount { get; init; }
    }
    public sealed record ManaCurveBucket
    {
        public required string Label { get; init; }
        public int Count { get; init; }

        public double RelativeHeight { get; init; }
    }
}
