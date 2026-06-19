using CollectaMundo.ApplicationServices.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.Shared.CardModels
{
    public sealed class PrintingCard : ICardListSortable, ICardImageSourceCard
    {
        public required OracleCard Oracle { get; init; }

        public string? Colors => Oracle.Colors;
        public decimal? EtchedPrice => CardDataProviders.PriceMetaProvider?.Get(Uuid)?.EtchedPrice;
        public string? Finishes { get; init; }
        public IReadOnlyList<string> FinishOptions =>
            string.IsNullOrWhiteSpace(Finishes)
                ? []
                : Finishes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        public decimal? FoilPrice => CardDataProviders.PriceMetaProvider?.Get(Uuid)?.FoilPrice;

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
        public bool IsOnlineOnly { get; init; }
        public string? Keywords => Oracle.Keywords;
        public string? Language { get; init; }
        public string? ManaCost => Oracle.ManaCost;
        public ImageSource? ManaCostImage => Oracle.ManaCostImage;
        public string? ManaCostRaw => Oracle.ManaCostRaw;
        public double ManaValue => Oracle.ManaValue;
        public string Name => Oracle.Name;
        public decimal? NormalPrice => CardDataProviders.PriceMetaProvider?.Get(Uuid)?.NormalPrice;
        public IReadOnlyList<string> OtherFaceIds => Oracle.OtherFaceIds;
        public List<string> OtherLanguages { get; set; } = [];
        public string? Rarity { get; init; }

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
        public string? ScryfallOracleId => Oracle.ScryfallOracleId;
        public string? SetCode { get; init; }

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
        public string? Side => Oracle.Side;
        public string? SubTypes => Oracle.SubTypes;
        public string? SuperTypes => Oracle.SuperTypes;
        public string? Text => Oracle.Text;
        public string? Type => Oracle.Type;
        public string? Types => Oracle.Types;
        public required string Uuid { get; init; }
    }
}
