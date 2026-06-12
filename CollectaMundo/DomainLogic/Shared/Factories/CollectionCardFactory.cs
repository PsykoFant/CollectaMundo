using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared.Factories
{
    public static class CollectionCardFactory
    {
        public static CollectionCard FromPrintingAndDbRow(PrintingCard printing, CollectionCardDbRow row)
        {
            var identity = row.Identity;

            return new CollectionCard
            {
                Printing = printing,
                CardId = row.CardId,
                CardsOwned = row.CardsOwned,
                CardsForTrade = row.CardsForTrade,
                SelectedCondition = identity.Condition,
                Language = identity.Language,
                SelectedFinish = identity.Finish,
                SelectedLocationId = identity.LocationId,
                Comment = identity.Comment
            };
        }
    }
}
