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
        public required PrintingCard Printing { get; init; }
        public int CardId { get; init; }
        public string Uuid => Printing.Uuid;
        public OracleCard Oracle => Printing.Oracle;
        public string Name => Printing.Name;
        public string? ScryfallOracleId => Printing.ScryfallOracleId;
        public string? ManaCost => Printing.ManaCost;
        public string? ManaCostRaw => Printing.ManaCostRaw;
        public string? Colors => Printing.Colors;
        public string? Type => Printing.Type;
        public string? Types => Printing.Types;
        public string? SuperTypes => Printing.SuperTypes;
        public string? SubTypes => Printing.SubTypes;
        public string? Keywords => Printing.Keywords;
        public string? Text => Printing.Text;
        public string? Side => Printing.Side;
        public double ManaValue => Printing.ManaValue;
        public string? SetCode => Printing.SetCode;
        public string? Rarity => Printing.Rarity;
        public ImageSource? ManaCostImage => Printing.ManaCostImage;
        public ImageSource? KeyRuneImage => Printing.KeyRuneImage;
        public string? SetName => Printing.SetName;
        public DateTime? ReleaseDate => Printing.ReleaseDate;
        public decimal? NormalPrice => Printing.NormalPrice;
        public decimal? FoilPrice => Printing.FoilPrice;
        public decimal? EtchedPrice => Printing.EtchedPrice;

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
        public IKeyedDataProvider<int, CardLocation>? CardLocationProvider { get; set; }
        public string? SelectedLocationName =>
            SelectedLocationId is int id
                ? CardLocationProvider?.Get(id)?.Name
                : null;

        public string? SelectedLocationDisplayName =>
            SelectedLocationId is int id
                ? CardLocationProvider?.Get(id)?.DisplayName
                : null;

        public CardLocationType? SelectedLocationType =>
            SelectedLocationId is int id
                ? CardLocationProvider?.Get(id)?.Type
                : null;

        [ObservableProperty]
        private string? comment;
        public decimal? CardInCollectionPrice =>
            (SelectedFinish ?? "").Equals("foil", StringComparison.OrdinalIgnoreCase) ? FoilPrice :
            (SelectedFinish ?? "").Equals("etched", StringComparison.OrdinalIgnoreCase) ? EtchedPrice :
            NormalPrice;
        public int Count { get; set; }
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
