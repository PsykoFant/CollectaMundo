namespace CollectaMundo.DomainLogic.Decks.Models.Records
{
    public sealed record DeckStats
    {
        public int CardCount { get; init; }
        public int NonLandCardCount { get; init; }


        public double CreaturePercentage { get; init; }
        public double LandPercentage { get; init; }
        public double SpellPercentage { get; init; }

        public IReadOnlyList<DeckStatsBucket> TypeBreakdown { get; init; } = [];
        public IReadOnlyList<DeckStatsBucket> ColorBreakdown { get; init; } = [];
        public IReadOnlyList<DeckStatsBucket> ManaCurve { get; init; } = [];
        public int ManaCurveMaxCount { get; init; }
    }
}
