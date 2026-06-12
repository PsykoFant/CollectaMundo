using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public interface ICollectionMutationsService
    {
        Task<CollectionChangeSet<CollectionCardDbRow>> SubmitBatchAsync(IEnumerable<CollectionCardDraft> cards, ICollectionSnapshot snapshot, SQLiteConnection connection, SQLiteTransaction transaction);
    }
}
