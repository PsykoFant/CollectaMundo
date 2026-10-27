using CollectaMundo.ApplicationServices.CardImages.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.KeyedDataProvider;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed partial class CollectionCard : ObservableObject, ICardListSortable, ICardImageSourceCard
    {
        // ---------------------------------------------------------
        // Printing
        // ---------------------------------------------------------

        public required PrintingCard Printing { get; init; }
        public string Uuid => Printing.Uuid;
        public string? SetCode => Printing.SetCode;
        public string? Rarity => Printing.Rarity;
        public string? Availability => Printing.Availability;
        public ImageSource? KeyRuneImage => Printing.KeyRuneImage;
        public string? SetName => Printing.SetName;
        public DateTime? ReleaseDate => Printing.ReleaseDate;
        public decimal? NormalPrice => Printing.NormalPrice;
        public decimal? FoilPrice => Printing.FoilPrice;
        public decimal? EtchedPrice => Printing.EtchedPrice;


        // ---------------------------------------------------------
        // Oracle
        // ---------------------------------------------------------
        public OracleCard Oracle => Printing.Oracle;
        public string Name => Oracle.Name;
        public string? ScryfallOracleId => Oracle.ScryfallOracleId;
        public string? ManaCost => Oracle.ManaCost;
        public string? ManaCostRaw => Oracle.ManaCostRaw;
        public ImageSource? ManaCostImage => Oracle.ManaCostImage;
        public double ManaValue => Oracle.ManaValue;
        public string? Colors => Oracle.Colors;
        public int GamePlayCard => Oracle.GamePlayCard;
        public string? Type => Oracle.Type;
        public string? Types => Oracle.Types;
        public string? SuperTypes => Oracle.SuperTypes;
        public string? SubTypes => Oracle.SubTypes;
        public string? Keywords => Oracle.Keywords;
        public string? Text => Oracle.Text;
        public string? Side => Printing.Side;


        // ---------------------------------------------------------
        // Collection identity
        // ---------------------------------------------------------

        public int CardId { get; init; }

        // ---------------------------------------------------------
        // Collection state
        // ---------------------------------------------------------

        [ObservableProperty]
        private string? selectedCondition;

        [ObservableProperty]
        private string? selectedFinish;

        [ObservableProperty]
        private string? language;

        [ObservableProperty]
        private int cardsOwned;

        [ObservableProperty]
        private int cardsForTrade;

        [ObservableProperty]
        private int? selectedLocationId;

        [ObservableProperty]
        private string? comment;


        // ---------------------------------------------------------
        // Location
        // ---------------------------------------------------------

        public IKeyedDataProvider<int, CardLocation>? CardLocationProvider
        {
            get;
            set;
        }
        public string? SelectedLocationName => SelectedLocationId is int id ? CardLocationProvider?.Get(id)?.Name : null;
        public string? SelectedLocationDisplayName => SelectedLocationId is int id ? CardLocationProvider?.Get(id)?.DisplayName : null;
        public CardLocationType? SelectedLocationType => SelectedLocationId is int id ? CardLocationProvider?.Get(id)?.Type : null;

        // ---------------------------------------------------------
        // Derived collection values
        // ---------------------------------------------------------

        public decimal? CardInCollectionPrice => (SelectedFinish ?? "").Equals("foil", StringComparison.OrdinalIgnoreCase)
                ? FoilPrice
                : (SelectedFinish ?? "").Equals("etched", StringComparison.OrdinalIgnoreCase) ? EtchedPrice : NormalPrice;

        public int Count { get; set; }

        // ---------------------------------------------------------
        // Change handling
        // ---------------------------------------------------------
        partial void OnSelectedLocationIdChanged(int? value)
        {
            OnPropertyChanged(nameof(SelectedLocationName));
            OnPropertyChanged(nameof(SelectedLocationType));
            OnPropertyChanged(nameof(SelectedLocationDisplayName));
        }
        partial void OnSelectedFinishChanged(string? value)
        {
            RecomputeCollectionPrice();
        }


        // ---------------------------------------------------------
        // Refresh
        // ---------------------------------------------------------

        public void RecomputeCollectionPrice()
        {
            OnPropertyChanged(nameof(CardInCollectionPrice));
        }
        public void RefreshLocationsFromProvider()
        {
            OnPropertyChanged(nameof(SelectedLocationName));
            OnPropertyChanged(nameof(SelectedLocationType));
            OnPropertyChanged(nameof(SelectedLocationDisplayName));
        }
    }
}
