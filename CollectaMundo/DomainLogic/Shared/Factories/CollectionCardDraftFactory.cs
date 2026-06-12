using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared.Factories
{
    public static class CollectionCardDraftFactory
    {
        // From CollectionCard to CollectionCardDraft - full 
        public static CollectionCardDraft FromCollectionCard(CollectionCard source)
        {
            return new CollectionCardDraft
            {
                CardId = source.CardId,
                Uuid = source.Uuid,

                SelectedCondition = source.SelectedCondition,
                Language = source.Language,
                SelectedFinish = source.SelectedFinish,
                SelectedLocationId = source.SelectedLocationId,
                Comment = source.Comment,

                CardsOwned = source.CardsOwned,
                CardsForTrade = source.CardsForTrade
            };
        }

        // From CollectionCard to CollectionCardDraft - partial for specific operations
        public static CollectionCardDraft FromCollectionCardForDelete(CollectionCard source)
        {
            var draft = FromCollectionCard(source);

            draft.CardsOwned = 0;
            draft.CardsForTrade = 0;

            return draft;
        }
        public static CollectionCardDraft FromCollectionCardForTradeAll(CollectionCard source)
        {
            var draft = FromCollectionCard(source);

            draft.CardsForTrade = draft.CardsOwned;

            return draft;
        }
        public static CollectionCardDraft FromCollectionCardForTradeNone(CollectionCard source)
        {
            var draft = FromCollectionCard(source);

            draft.CardsForTrade = 0;

            return draft;
        }
        public static CollectionCardDraft FromCollectionCardWithLocation(CollectionCard source, int? locationId)
        {
            var draft = FromCollectionCard(source);

            draft.SelectedLocationId = locationId;

            return draft;
        }


        // From CollectionCardDbRow to CollectionCardDraft - full
        public static CollectionCardDraft FromDbRow(CollectionCardDbRow row)
        {
            return new CollectionCardDraft
            {
                CardId = row.CardId,
                Uuid = row.Identity.Uuid,
                SelectedCondition = row.Identity.Condition,
                Language = row.Identity.Language,
                SelectedFinish = row.Identity.Finish,
                SelectedLocationId = row.Identity.LocationId,
                Comment = row.Identity.Comment,
                CardsOwned = row.CardsOwned,
                CardsForTrade = row.CardsForTrade
            };
        }

        // From CollectionCardDbRow to CollectionCardDraft - partial for specific operations
        public static CollectionCardDraft FromDbRowWithClearedLocation(CollectionCardDbRow row)
        {
            var draft = FromDbRow(row);

            draft.SelectedLocationId = null;

            return draft;
        }


        // From PrintingCard to CollectionCardDraft - partial for creating new collection entries with default values
        public static CollectionCardDraft FromPrintingCard(PrintingCard source)
        {
            return new CollectionCardDraft
            {
                Uuid = source.Uuid,
                Name = source.Name,
                SetName = source.SetName,
                CardsOwned = 1,
                CardsForTrade = 0
            };
        }

        // From CollectionCardDraft to CollectionCardDraft - for splitting cards in the collection
        public static CollectionCardDraft FromSplit(CollectionCardDraft source)
        {
            return new CollectionCardDraft
            {
                CardId = null,

                Uuid = source.Uuid,
                Name = source.Name,
                SetName = source.SetName,

                CardsOwned = 1,
                CardsForTrade = source.CardsOwned > source.CardsForTrade ? 0 : 1,

                Conditions = source.Conditions,
                FinishOptions = source.FinishOptions,
                OtherLanguages = source.OtherLanguages,

                SelectedCondition = source.SelectedCondition,
                Language = source.Language,
                SelectedFinish = source.SelectedFinish,
                SelectedLocationId = source.SelectedLocationId,
                Comment = source.Comment
            };
        }
    }
}
