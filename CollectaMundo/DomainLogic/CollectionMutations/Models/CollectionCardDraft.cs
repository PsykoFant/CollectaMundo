using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.CollectionMutations.Models
{
    public sealed partial class CollectionCardDraft : ObservableObject
    {
        public int? CardId { get; set; }
        public required string Uuid { get; init; }

        [ObservableProperty]
        private int cardsOwned;

        [ObservableProperty]
        private int cardsForTrade;

        [ObservableProperty]
        private string? selectedCondition;

        [ObservableProperty]
        private string? language;

        [ObservableProperty]
        private string? selectedFinish;

        [ObservableProperty]
        private int? selectedLocationId;

        [ObservableProperty]
        private string? comment;

        public CollectionIdentity ToIdentity()
        {
            return CollectionIdentityFactory.Create(Uuid, SelectedCondition, Language, SelectedFinish, SelectedLocationId, Comment);
        }
    }
}
