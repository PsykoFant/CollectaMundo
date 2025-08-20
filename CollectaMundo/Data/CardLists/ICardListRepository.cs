using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Data.CardLists
{
    public interface ICardListRepository
    {
        Task<IReadOnlyList<CardCore>> ReadAllCardsCoresAsync(SQLiteConnection conn);
        Task<List<MyCollectionRow>> ReadMyCollectionAsync(SQLiteConnection conn);
        Task<IReadOnlyDictionary<string, byte[]>> ReadManaCostImagesAsync(SQLiteConnection conn);

        // old
        Task<IReadOnlyList<CardSet>> QueryAsync(string sql, SQLiteConnection conn, Func<DbDataReader, CardSet> map);
    }

}
