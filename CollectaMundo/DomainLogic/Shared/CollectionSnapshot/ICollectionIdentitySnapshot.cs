using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public interface ICollectionIdentitySnapshot
    {
        bool TryGetById(int cardId, out CollectionCardDbRow row);
        bool TryGetByIdentity(CollectionIdentity identity, out CollectionCardDbRow row);
        IReadOnlyCollection<CollectionCardDbRow> Rows { get; }
    }
}
