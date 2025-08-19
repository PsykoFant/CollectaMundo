using CollectaMundo.DomainLogic.CardLists.Images;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class CardSet : INotifyPropertyChanged   // sealed for small perf win; preserves public API
    {
        // in CardSet.cs (top of class)
        public static IManaCostImageProvider? ManaCostImageProvider { get; set; }

        // ========= NEW: shared core payload (built once per UUID) =========
        public CardCore? Core { get; private set; }

        // ========= Existing fields preserved (public API unchanged) =========
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
        public DateTime? ReleaseDate { get; set; }
        public string? SetCode { get; set; }
        public string? SetName { get; set; }
        public string? Side { get; set; }
        public List<string>? Subsets { get; set; }
        public string? SubTypes { get; set; }
        public string? SuperTypes { get; set; }
        public string? Text { get; set; }
        public string? Toughness { get; set; }
        public string? Type { get; set; }
        public string? Types { get; set; }
        public string? Uuid { get; set; }

        // ======== Images (lazy decode preserved; bytes shared via Core) ========
        private ImageSource? _keyRuneImage;
        public ImageSource? KeyRuneImage
        {
            get
            {
                if (_keyRuneImage == null && KeyRuneImageBytes != null)
                {
                    _keyRuneImage = ConvertImage(KeyRuneImageBytes);
                }
                return _keyRuneImage;
            }
            set => _keyRuneImage = value;
        }

        // Keep property for compatibility; value references Core’s byte[] set by factories
        public byte[]? KeyRuneImageBytes => Core?.KeyRuneImageBytes;   // forwarder to shared core

        public string? ManaCostRaw { get; set; }

        private ImageSource? _manaCostImage;
        public ImageSource? ManaCostImage
        {
            get
            {
                _manaCostImage ??= ManaCostImageProvider?.GetImage(Core?.ManaCostRaw ?? ManaCostRaw);
                return _manaCostImage;
            }
            set => _manaCostImage = value;
        }
        private static BitmapImage? ConvertImage(byte[] imageData)
        {
            try
            {
                using (MemoryStream ms = new(imageData))
                {
                    BitmapImage image = new();
                    ms.Position = 0;
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to convert image: {ex.Message}");
                return null;
            }
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

        // ========= NEW: factory helpers to construct from a shared Core =========

        /// <summary>
        /// Build an AllCards entry from a shared CardCore. Public surface stays the same.
        /// </summary>
        public static CardSet FromCore(CardCore core)
        {
            var c = new CardSet
            {
                Core = core,

                // Assign public props from Core so existing filters/bindings keep working
                Uuid = core.Uuid,
                Name = core.Name,
                SetName = core.SetName,
                ReleaseDate = core.ReleaseDate,
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

        /// <summary>
        /// Build a MyCollection entry by overlaying per-user fields on top of a shared Core.
        /// </summary>
        public static CardSet FromCoreWithCollection(
            CardCore core,
            int cardId,
            int cardsOwned,
            int cardsForTrade,
            string? condition,
            string? language,
            string? finish)
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

        // ========= NEW: helper to recompute collection price on finish change =========
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
