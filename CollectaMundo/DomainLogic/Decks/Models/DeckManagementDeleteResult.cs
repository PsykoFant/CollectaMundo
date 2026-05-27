using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed record DeckManagementDeleteResult(OperationResult Result, CollectionChangeSet<CardSet> CollectionChangeSet);
}
