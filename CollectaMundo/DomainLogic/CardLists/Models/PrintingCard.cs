using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class PrintingCard : ICardListSortable
    {
        public required OracleCard Oracle { get; init; }
        public required string Uuid { get; init; }
        public string? SetCode { get; init; }
        public string? Language { get; init; }
        public string? Rarity { get; init; }
        public string? Finishes { get; init; }
        public string Name => Oracle.Name;
        public string? ScryfallOracleId => Oracle.ScryfallOracleId;
        public string? ManaCost => Oracle.ManaCost;
        public string? ManaCostRaw => Oracle.ManaCostRaw;
        public string? Colors => Oracle.Colors;
        public string? Type => Oracle.Type;
        public string? Types => Oracle.Types;
        public string? SuperTypes => Oracle.SuperTypes;
        public string? SubTypes => Oracle.SubTypes;
        public string? Keywords => Oracle.Keywords;
        public string? Text => Oracle.Text;
        public string? Side => Oracle.Side;
        public IReadOnlyList<string> OtherFaceIds => Oracle.OtherFaceIds;
        public double ManaValue => Oracle.ManaValue;
        public ImageSource? ManaCostImage => Oracle.ManaCostImage;

        private ImageSource? _keyRuneImage;
        public ImageSource? KeyRuneImage
        {
            get
            {
                if (_keyRuneImage is null && !string.IsNullOrWhiteSpace(SetCode))
                {
                    _keyRuneImage = CardDataProviders.SetIconImages?.Get(SetCode);
                }

                return _keyRuneImage;
            }
        }

        private string? _setName;

        private bool _setNameCached;
        public string? SetName
        {
            get
            {
                if (_setNameCached)
                {
                    return _setName;
                }

                _setNameCached = true;

                if (string.IsNullOrWhiteSpace(SetCode))
                {
                    return null;
                }

                _setName = CardDataProviders.SetMetaProvider?.Get(SetCode)?.Name;
                return _setName;
            }
        }

        private DateTime? _releaseDate;
        public DateTime? ReleaseDate
        {
            get
            {
                if (_releaseDate.HasValue)
                {
                    return _releaseDate;
                }

                if (string.IsNullOrWhiteSpace(SetCode))
                {
                    return null;
                }

                _releaseDate = CardDataProviders.SetMetaProvider?.Get(SetCode)?.ReleaseDate;
                return _releaseDate;
            }
        }
        public decimal? NormalPrice => CardDataProviders.PriceMetaProvider?.Get(Uuid)?.NormalPrice;
        public decimal? FoilPrice => CardDataProviders.PriceMetaProvider?.Get(Uuid)?.FoilPrice;
        public decimal? EtchedPrice => CardDataProviders.PriceMetaProvider?.Get(Uuid)?.EtchedPrice;
    }
}
