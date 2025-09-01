using CollectaMundo.ApplicationServices.Utilities;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class CardSet : INotifyPropertyChanged
    {
        public static IImageProvider<string>? ManaCostImages { get; set; }
        public static IImageProvider<string>? SetIconImages { get; set; }
        public static IValueProvider<string, SetMeta>? SetMetaProvider { get; set; }


        // shared core payload
        public CardCore? Core { get; private set; }


        public string? Artist { get; set; }
        public List<string>? ArtistIds { get; set; }
        public string? BorderColor { get; set; }
        public List<string>? CardParts { get; set; }
        public List<string>? ColorIdentity { get; set; }
        public List<string>? ColorIndicator { get; set; }
        public string? Colors { get; set; }
        public double? ConvertedManaCost { get; set; }
        public string? Defense { get; set; }
        public double? FaceConvertedManaCost { get; set; }
        public double? FaceManaValue { get; set; }
        public string? FaceName { get; set; }
        public string? Finishes { get; set; }
        public string? FlavorName { get; set; }
        public string? FlavorText { get; set; }
        public bool? HasNonFoil { get; set; }
        public bool? IsAlternative { get; set; }
        public bool? IsFullArt { get; set; }
        public bool? IsFunny { get; set; }
        public bool? IsOnlineOnly { get; set; }
        public bool? IsOversized { get; set; }
        public bool? IsPromo { get; set; }
        public bool? IsRebalanced { get; set; }
        public bool? IsReprint { get; set; }
        public bool? IsReserved { get; set; }
        public bool? IsStarter { get; set; }
        public bool? IsStorySpotlight { get; set; }
        public bool? IsTextless { get; set; }
        public string? Keywords { get; set; }
        public string? Language { get; set; }
        public List<string>? OtherLanguages { get; set; }
        public string? Layout { get; set; }
        public string? Life { get; set; }
        public string? Loyalty { get; set; }
        public string? ManaCost { get; set; }
        public double ManaValue { get; set; }
        public string? Name { get; set; }
        public string? Number { get; set; }
        public List<string>? OtherFaceIds { get; set; }
        public string? Power { get; set; }
        public List<string>? PromoTypes { get; set; }
        public string? Rarity { get; set; }
        public List<string>? RebalancedPrintings { get; set; }
        public DateTime? ReleaseDate
        {
            get
            {
                var code = SetCode ?? Core?.SetCode;
                if (string.IsNullOrWhiteSpace(code)) return null;
                return SetMetaProvider?.Get(code)?.ReleaseDate;
            }
        }
        public string? SetCode { get; set; }
        public string? SetName
        {
            get
            {
                var code = SetCode ?? Core?.SetCode;
                if (string.IsNullOrWhiteSpace(code)) return null;
                return SetMetaProvider?.Get(code)?.Name;
            }
        }
        public string? Side { get; set; }
        public List<string>? Subsets { get; set; }
        public string? SubTypes { get; set; }
        public string? SuperTypes { get; set; }
        public string? Text { get; set; }
        public string? Toughness { get; set; }
        public string? Type { get; set; }
        public string? Types { get; set; }
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
                        _keyRuneImage = SetIconImages?.GetImage(key);
                }
                return _keyRuneImage;
            }
            set => _keyRuneImage = value;
        }
        public string? ManaCostRaw { get; set; }

        private ImageSource? _manaCostImage;
        public ImageSource? ManaCostImage
        {
            get
            {
                if (_manaCostImage == null)
                {
                    _manaCostImage = ManaCostImages?.GetImage(Core?.ManaCostRaw ?? ManaCostRaw ?? string.Empty);
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
                    // Optional: recompute CardInCollectionPrice when finish changes
                    RecomputeCollectionPrice();
                }
            }
        }

        public decimal? CardInCollectionPrice { get; set; }

        // ======== Prices (preserved) ========
        public decimal? NormalPrice { get; set; }
        public decimal? FoilPrice { get; set; }
        public decimal? EtchedPrice { get; set; }

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

                NormalPrice = core.NormalPrice,
                FoilPrice = core.FoilPrice,
                EtchedPrice = core.EtchedPrice
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

            // Initial price compute based on finish (you can keep your existing logic if different)
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

        // helper to recompute collection price on finish change 
        private void RecomputeCollectionPrice()
        {
            CardInCollectionPrice = SelectedFinish?.ToLowerInvariant() switch
            {
                "foil" => FoilPrice,
                "etched" => EtchedPrice,
                _ => NormalPrice
            };
            OnPropertyChanged(nameof(CardInCollectionPrice));
        }
    }
}
