using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLists
{
    public interface ICardListRepo
    {
        Task<IReadOnlyList<CardCoreDto>> ReadAllCardsCoreDtosAsync(SQLiteConnection conn);
        Task<List<MyCollectionRow>> ReadMyCollectionAsync(SQLiteConnection conn);
    }

}
