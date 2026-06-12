using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared
{
    public interface ICollectionSnapshot
    {
        bool TryGetById(int cardId, out CollectionCardDbRow row);
        bool TryGetByIdentity(CollectionIdentity identity, out CollectionCardDbRow row);
        IReadOnlyCollection<CollectionCardDbRow> Rows { get; }
    }
}
