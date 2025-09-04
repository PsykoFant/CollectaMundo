using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.Data.CardLists
{
    public interface ICardListRepository
    {
        Task<IReadOnlyList<CardCoreDto>> ReadAllCardsCoreDtosAsync(SQLiteConnection conn);
        Task<List<MyCollectionRow>> ReadMyCollectionAsync(SQLiteConnection conn);
    }

}
