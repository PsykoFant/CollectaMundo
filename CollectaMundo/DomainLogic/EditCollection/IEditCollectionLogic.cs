using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
using System.Data.SQLite;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public interface IEditCollectionLogic
    {
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit, SQLiteConnection connection);
        Task<IReadOnlyList<CardChangeEventArgs>> SaveBatchAsync(IEnumerable<CardSet> cards, bool isEdit, SQLiteConnection connection);
    }
}
