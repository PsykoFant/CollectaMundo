using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public interface ICollectionMutationsService
    {
        Task<CollectionChangeSet<CollectionCardDbRow>> SubmitBatchAsync(IEnumerable<CollectionCardDraft> cards, ICollectionIdentitySnapshot snapshot, SQLiteConnection connection, SQLiteTransaction transaction);
    }
}
