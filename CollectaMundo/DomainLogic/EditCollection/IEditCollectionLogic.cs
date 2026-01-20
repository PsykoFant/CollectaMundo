using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Data.SQLite;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public interface IEditCollectionLogic
    {
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit, SQLiteConnection connection);
        Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard, SQLiteConnection connection);
        EditBatchPlan PlanBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit);
    }
}
