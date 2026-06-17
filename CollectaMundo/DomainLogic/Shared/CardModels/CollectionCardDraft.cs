using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.KeyedDataProvider;
using CollectaMundo.DomainLogic.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.CollectionMutations.Models
{
    public sealed partial class CollectionCardDraft : ObservableObject
    {
        public int? CardId { get; set; }
        public required string Uuid { get; init; }
        public string? Name { get; init; }
        public string? SetName { get; init; }

        [ObservableProperty]
        private int cardsOwned;

        [ObservableProperty]
        private int cardsForTrade;

        [ObservableProperty]
        private string? selectedCondition;
        public IReadOnlyList<string> Conditions { get; init; } = ConditionOptions.Values;

        [ObservableProperty]
        private string? selectedFinish;
        public IReadOnlyList<string> FinishOptions { get; init; } = [];

        [ObservableProperty]
        private string? language;
        public IReadOnlyList<string> OtherLanguages { get; init; } = [];

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
        partial void OnSelectedLocationIdChanged(int? value)
        {
            RefreshLocationsFromProvider();
        }

        public void RefreshLocationsFromProvider()
        {
            OnPropertyChanged(nameof(SelectedLocationName));
            OnPropertyChanged(nameof(SelectedLocationType));
            OnPropertyChanged(nameof(SelectedLocationDisplayName));
        }

        [ObservableProperty]
        private string? comment;
    }
}
