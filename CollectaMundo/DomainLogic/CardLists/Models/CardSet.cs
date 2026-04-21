using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.KeyedDataProvider;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public partial class CardSet : ObservableObject
    {
        // -------------------------------
        // Static providers (shared metadata + images)
        // -------------------------------
        public static IKeyedDataProvider<string, ImageSource>? ManaCostImages { get; set; }
        public static IKeyedDataProvider<string, ImageSource>? SetIconImages { get; set; }
        public static IKeyedDataProvider<string, SetDto>? SetMetaProvider { get; set; }
        public static IKeyedDataProvider<string, PriceDto>? PriceMetaProvider { get; set; }
        public static IKeyedDataProvider<int, CardLocation>? CardLocationProvider { get; set; }

        // -------------------------------
        // Core reference
        // -------------------------------
        public CardCore? Core { get; private set; }

        // -------------------------------
        // Hydrated from CardCore
        // -------------------------------
        public string? Uuid { get; set; }
        public string? Name { get; init; }
        public string? SetCode { get; init; }
        public string? ManaCost { get; init; }
        public string? ManaCostRaw { get; init; }
        public string? Colors { get; init; }
        public string? Type { get; init; }
        public string? Types { get; init; }
        public string? SuperTypes { get; init; }
        public string? SubTypes { get; init; }
        public string? Keywords { get; init; }
        public string? Text { get; init; }
        public string? Side { get; init; }
        public string? Rarity { get; init; }
        public string? Finishes { get; init; }
        public string? Language { get; set; }
        public List<string> OtherLanguages { get; set; } = [];
        public double ManaValue { get; init; }

        // -------------------------------
        // Metadata derived via lookup (memoized)
        // -------------------------------
        private string? _resolvedSetCode;
        private bool _resolvedSetCodeCached;
        private string? ResolvedSetCode
        {
            get
            {
                if (_resolvedSetCodeCached)
                {
                    return _resolvedSetCode;
                }

                _resolvedSetCodeCached = true;
                _resolvedSetCode = SetCode ?? Core?.SetCode;
                return _resolvedSetCode;
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

                var code = ResolvedSetCode;
                if (string.IsNullOrWhiteSpace(code))
                {
                    return null;
                }

                _setName = SetMetaProvider?.Get(code)?.Name;
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

                var code = ResolvedSetCode;
                if (string.IsNullOrWhiteSpace(code))
                {
                    return null;
                }

                _releaseDate = SetMetaProvider?.Get(code)?.ReleaseDate;
                return _releaseDate;
            }
        }

        // -------------------------------
        // Image properties (memoized)
        // -------------------------------
        private ImageSource? _keyRuneImage;
        public ImageSource? KeyRuneImage
        {
            get
            {
                if (_keyRuneImage == null)
                {
                    var code = ResolvedSetCode;
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        _keyRuneImage = SetIconImages?.Get(code);
                    }
                }
                return _keyRuneImage;
            }
            set => _keyRuneImage = value;
        }

        private ImageSource? _manaCostImage;
        public ImageSource? ManaCostImage
        {
            get
            {
                if (_manaCostImage == null)
                {
                    var key = Core?.ManaCostRaw ?? ManaCostRaw ?? string.Empty;
                    _manaCostImage = ManaCostImages?.Get(key);
                }
                return _manaCostImage;
            }
            set => _manaCostImage = value;
        }

        // -------------------------------
        // Collection & user-specific state
        // -------------------------------
        public int? CardId { get; set; }

        // Condition       
        [ObservableProperty]
        private string? selectedCondition;
        public List<string> Conditions { get; } = ["Mint", "Near Mint", "Excellent", "Good","Light Played", "Played", "Poor"];

        // Finish
        [ObservableProperty]
        private string? selectedFinish;
        public List<string> AvailableFinishes { get; set; } = [];

        // Owned and for trade counts
        [ObservableProperty]
        private int cardsOwned;

        [ObservableProperty]
        private int cardsForTrade;

        // Location
        [ObservableProperty]
        private int? selectedLocationId;
        public string? SelectedLocationName => SelectedLocationId is int id ? CardLocationProvider?.Get(id)?.Name : null;
        public string? SelectedLocationDisplayName => SelectedLocationId is int id ? CardLocationProvider?.Get(id)?.DisplayName : null;
        public CardLocationType? SelectedLocationType => SelectedLocationId is int id ? CardLocationProvider?.Get(id)?.Type : null;

        // Comment
        [ObservableProperty]
        private string? comment;

        // -------------------------------
        // Price lookups (live from PriceMetaProvider)
        // -------------------------------
        public decimal? NormalPrice => PriceMetaProvider?.Get(Uuid ?? string.Empty)?.NormalPrice;
        public decimal? FoilPrice => PriceMetaProvider?.Get(Uuid ?? string.Empty)?.FoilPrice;
        public decimal? EtchedPrice => PriceMetaProvider?.Get(Uuid ?? string.Empty)?.EtchedPrice;

        public decimal? CardInCollectionPrice =>
            (SelectedFinish ?? "").Equals("foil", StringComparison.OrdinalIgnoreCase) ? FoilPrice :
            (SelectedFinish ?? "").Equals("etched", StringComparison.OrdinalIgnoreCase) ? EtchedPrice :
            NormalPrice;

        // -------------------------------
        // Deck-related state
        // -------------------------------
        public int Count { get; set; }

        // -------------------------------
        // Factory methods
        // -------------------------------
        public static CardSet FromCore(CardCore core)
        {
            var c = new CardSet
            {
                Core = core,
                Uuid = core.Uuid,
                Name = core.Name,
                SetCode = core.SetCode,
                ManaCost = core.ManaCost,
                ManaCostRaw = core.ManaCostRaw,
                Colors = core.Colors,
                Type = core.Type,
                Types = core.Types,
                SuperTypes = core.SuperTypes,
                SubTypes = core.SubTypes,
                Keywords = core.Keywords,
                Text = core.Text,
                Side = core.Side,
                Rarity = core.Rarity,
                Finishes = core.Finishes,
                ManaValue = core.ManaValue,
                Language = core.Language
            };

            return c;
        }
        public static CardSet FromCoreWithCollection(CardCore core,int cardId,int cardsOwned,int cardsForTrade,string? condition,string? language,string? finish,int? locationId,string? comment)
        {
            var c = FromCore(core);

            c.CardId = cardId;
            c.CardsOwned = cardsOwned;
            c.CardsForTrade = cardsForTrade;
            c.SelectedCondition = condition;
            c.Language = language ?? core.Language;
            c.SelectedFinish = finish;
            c.SelectedLocationId = locationId;
            c.Comment = comment;

            c.RecomputeCollectionPrice();

            return c;
        }
        public static CardSet FromManaKey(string key)
        {
            return new CardSet { ManaCostRaw = key };
        }

        // -------------------------------
        // Change tracking + refresh
        // -------------------------------
        public void RefreshPricesFromProvider()
        {
            OnPropertyChanged(nameof(NormalPrice));
            OnPropertyChanged(nameof(FoilPrice));
            OnPropertyChanged(nameof(EtchedPrice));
            OnPropertyChanged(nameof(CardInCollectionPrice));
        }
        public void RefreshLocationsFromProvider()
        {
            OnPropertyChanged(nameof(SelectedLocationName));
            OnPropertyChanged(nameof(SelectedLocationType));
            OnPropertyChanged(nameof(SelectedLocationDisplayName));
        }
        partial void OnSelectedLocationIdChanged(int? value)
        {
            OnPropertyChanged(nameof(SelectedLocationName));
            OnPropertyChanged(nameof(SelectedLocationType));
            OnPropertyChanged(nameof(SelectedLocationDisplayName));
        }

        // -------------------------------
        // Recompute derived values
        // -------------------------------
        public void RecomputeCollectionPrice()
        {
            OnPropertyChanged(nameof(CardInCollectionPrice));
        }
    }
}
