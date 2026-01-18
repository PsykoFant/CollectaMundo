using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Data.SQLite;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public interface IEditCollectionLogic
    {
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit, SQLiteConnection connection);
        Task<IReadOnlyList<CollectionChangeSet<CardSet>>> SaveBatchAsync(IEnumerable<CardSet> cards, bool isEdit, SQLiteConnection connection);
        Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard, SQLiteConnection connection);
        CollectionChangeSet<CardSet> CreateCollectionChangeSetFromEdits(IEnumerable<CollectionChangeSet<CardSet>> changeSets);
    }
}
