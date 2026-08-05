using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public sealed class CollectionIdentitySnapshot : ICollectionIdentitySnapshot
    {
        private readonly IReadOnlyDictionary<int, CollectionCardDbRow> _byId;

        private readonly IReadOnlyDictionary<CollectionIdentity, CollectionCardDbRow> _byIdentity;

        public IReadOnlyCollection<CollectionCardDbRow> Rows { get; }

        internal CollectionIdentitySnapshot(IReadOnlyDictionary<int, CollectionCardDbRow> byId, IReadOnlyDictionary<CollectionIdentity, CollectionCardDbRow> byIdentity, IReadOnlyCollection<CollectionCardDbRow> rows)
        {
            _byId = byId;
            _byIdentity = byIdentity;
            Rows = rows;
        }

        public bool TryGetById(int cardId, out CollectionCardDbRow row)
        {
            return _byId.TryGetValue(cardId, out row!);
        }

        public bool TryGetByIdentity(CollectionIdentity identity, out CollectionCardDbRow row)
        {
            return _byIdentity.TryGetValue(identity, out row!);
        }
    }
}
