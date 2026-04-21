using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.ApplicationServices.CardLocations.Models
{
    public sealed record CardLocationDeleteResult(OperationResult Result, CollectionChangeSet<CardSet> CollectionChangeSet);
}
