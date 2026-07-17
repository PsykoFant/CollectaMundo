using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.Shared.CardModels
{
    public sealed class OracleCard
    {
        public string? Colors { get; init; }
        public string? Keywords { get; init; }

        // Legalities
        public CardLegalityMasks LegalityMasks { get; init; }
        public ulong PlayableFormatsMask => LegalityMasks.PlayableFormatsMask;
        public ulong RestrictedFormatsMask => LegalityMasks.RestrictedFormatsMask;

        // Manacost 
        public string? ManaCost { get; init; }
        public string? ManaCostRaw { get; init; }

        private ImageSource? _manaCostImage;
        public ImageSource? ManaCostImage
        {
            get
            {
                _manaCostImage ??= CardDataProviders.ManaCostImages?.Get(ManaCostRaw ?? string.Empty);

                return _manaCostImage;
            }
        }
        public double ManaValue { get; init; }

        public int GamePlayCard { get; init; }
        public required string Name { get; init; }
        public IReadOnlyList<string> OtherFaceIds { get; init; } = [];
        public required string ScryfallOracleId { get; init; }
        public string? Side { get; init; }
        public string? SubTypes { get; init; }
        public string? SuperTypes { get; init; }
        public string? Text { get; init; }
        public string? Type { get; init; }
        public string? Types { get; init; }
    }
}
