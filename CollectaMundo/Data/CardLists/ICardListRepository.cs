using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Data.CardLists
{
    public interface ICardListRepository
    {
        Task<IReadOnlyList<CardCore>> QueryAllCardsCoresAsync(SQLiteConnection conn);
        Task<IReadOnlyList<CardSet>> QueryAsync(string sql, SQLiteConnection conn, Func<DbDataReader, CardSet> map);
    }

}
