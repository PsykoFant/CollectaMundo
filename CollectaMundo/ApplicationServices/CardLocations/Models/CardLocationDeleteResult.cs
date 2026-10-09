using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CardLists.Models;

namespace CollectaMundo.ApplicationServices.CardLocations.Models
{
    public sealed record CardLocationDeleteResult(OperationResult Result, CollectionChangeSet<CollectionCardDbRow> CollectionChangeSet);
}
