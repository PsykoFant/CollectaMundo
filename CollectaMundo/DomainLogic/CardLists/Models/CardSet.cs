using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class CardSet : INotifyPropertyChanged
    {
        // images
        public static ILookupProvider<string, ImageSource>? ManaCostImages { get; set; }
        public static ILookupProvider<string, ImageSource>? SetIconImages { get; set; }

        // metadata
        public static ILookupProvider<string, SetDto>? SetMetaProvider { get; set; }
        public static ILookupProvider<string, PriceDto>? PriceMetaProvider { get; set; }


        // shared core payload
        public CardCore? Core { get; private set; }


        public string? Colors { get; init; }
        public string? Finishes { get; init; }
        public string? Keywords { get; init; }
        public string? Language { get; set; }
        public List<string>? OtherLanguages { get; set; }
        public string? ManaCost { get; init; }
        public double ManaValue { get; init; }
        public string? Name { get; init; }
        public string? Rarity { get; init; }
        public DateTime? ReleaseDate
        {
            get
            {
                var code = SetCode ?? Core?.SetCode;
                if (string.IsNullOrWhiteSpace(code))
                {
                    return null;
                }

                return SetMetaProvider?.Get(code)?.ReleaseDate;
            }
        }
        public string? SetCode { get; init; }
        public string? SetName
        {
            get
            {
                var code = SetCode ?? Core?.SetCode;
                if (string.IsNullOrWhiteSpace(code))
                {
                    return null;
                }

                return SetMetaProvider?.Get(code)?.Name;
            }
        }
        public string? Side { get; init; }
        public string? SubTypes { get; init; }
        public string? SuperTypes { get; init; }
        public string? Text { get; init; }
        public string? Type { get; init; }
        public string? Types { get; init; }
        public string? Uuid { get; set; }

        private ImageSource? _keyRuneImage;
        public ImageSource? KeyRuneImage
        {
            get
            {
                if (_keyRuneImage == null)
                {
                    var key = Core?.SetCode ?? SetCode;
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        _keyRuneImage = SetIconImages?.Get(key);
                    }
                }
                return _keyRuneImage;
            }
            set => _keyRuneImage = value;
        }
        public string? ManaCostRaw { get; init; }

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
        public int? CardId { get; set; }

        // ========= INPC & per-collection fields (unchanged) =========
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private int _cardsOwned;
        public int CardsOwned
        {
            get => _cardsOwned;
            set
            {
                if (_cardsOwned != value)
                {
                    _cardsOwned = value;
                    OnPropertyChanged(nameof(CardsOwned));

                    if (CardsForTrade > _cardsOwned)
                    {
                        CardsForTrade = _cardsOwned;
                        OnPropertyChanged(nameof(CardsForTrade));
                    }
                }
            }
        }

        private int _cardsForTrade;
        public int CardsForTrade
        {
            get => _cardsForTrade;
            set
            {
                if (_cardsForTrade != value)
                {
                    _cardsForTrade = value < 0 ? 0 : value;
                    OnPropertyChanged(nameof(CardsForTrade));
                }
            }
        }

        private string? _selectedCondition;
        public List<string> Conditions { get; } =
        [
            "Mint",
            "Near Mint",
            "Excellent",
            "Good",
            "Light Played",
            "Played",
            "Poor"
        ];
        public string? SelectedCondition
        {
            get => _selectedCondition;
            set
            {
                if (_selectedCondition != value)
                {
                    _selectedCondition = value;
                    OnPropertyChanged(nameof(SelectedCondition));
                }
            }
        }
        public List<string> AvailableFinishes { get; set; } = [];

        private string? _selectedFinish;
        public string? SelectedFinish
        {
            get => _selectedFinish;
            set
            {
                if (_selectedFinish != value)
                {
                    _selectedFinish = value;
                    OnPropertyChanged(nameof(SelectedFinish));
                    // Recompute CardInCollectionPrice when finish changes
                    RecomputeCollectionPrice();
                }
            }
        }

        // ======== Prices ========
        public decimal? CardInCollectionPrice =>
            (SelectedFinish ?? "").Equals("foil", StringComparison.OrdinalIgnoreCase) ? FoilPrice :
            (SelectedFinish ?? "").Equals("etched", StringComparison.OrdinalIgnoreCase) ? EtchedPrice :
            NormalPrice;

        public decimal? NormalPrice => PriceMetaProvider?.Get(Uuid ?? string.Empty)?.NormalPrice;
        public decimal? FoilPrice => PriceMetaProvider?.Get(Uuid ?? string.Empty)?.FoilPrice;
        public decimal? EtchedPrice => PriceMetaProvider?.Get(Uuid ?? string.Empty)?.EtchedPrice;


        // ======== Deck field (preserved) ========
        public int Count { get; set; }

        // Factory helpers to construct from a shared Core 

        // Build an AllCards entry from a shared CardCore. Public surface stays the same.
        public static CardSet FromCore(CardCore core)
        {
            var c = new CardSet
            {
                Core = core,

                // Assign public props from Core so existing filters/bindings keep working
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
                Language = core.Language,
            };

            return c;
        }

        // Build a MyCollection entry by overlaying per-user fields on top of a shared Core.
        public static CardSet FromCoreWithCollection(CardCore core, int cardId, int cardsOwned, int cardsForTrade, string? condition, string? language, string? finish)
        {
            var c = FromCore(core);

            c.CardId = cardId;
            c.CardsOwned = cardsOwned;
            c.CardsForTrade = cardsForTrade;
            c.SelectedCondition = condition;
            c.Language = language ?? core.Language;
            c.SelectedFinish = finish;

            // Initial price compute based on finish
            c.RecomputeCollectionPrice();

            return c;
        }
        public static CardSet FromManaKey(string key)
        {
            return new CardSet
            {
                ManaCostRaw = key
            };
        }
        public void RefreshPricesFromProvider()
        {
            OnPropertyChanged(nameof(NormalPrice));
            OnPropertyChanged(nameof(FoilPrice));
            OnPropertyChanged(nameof(EtchedPrice));
            RecomputeCollectionPrice();
        }

        // helper to recompute collection price on finish change 
        public void RecomputeCollectionPrice()
        {
            OnPropertyChanged(nameof(CardInCollectionPrice));
        }
    }
}
