using CollectaMundo.DomainLogic.CollectionMutations.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public interface ICollectionMutationsService
    {
        Task ExecutePlanAsync(CollectionMutationPlan plan, SQLiteConnection connection);
    }
}
